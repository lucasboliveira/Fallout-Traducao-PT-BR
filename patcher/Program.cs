using System.Diagnostics;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// =====================================================================
//  Tradutor pt-BR do Fallout Shelter — patcher de 1 clique
//  Aplica a tradução (embutida) no data.unity3d da cópia do próprio
//  usuário. Faz backup automático. Renomeia o slot p/ "Português (Brasil)".
// =====================================================================

// O Fallout Shelter PC não tem menu de idioma: ele escolhe a partir da Steam
// (LocalizationManager.SelectStartupLanguage). Idiomas não mapeados caem no
// fallback do slot 0 (English). Por isso gravamos a tradução em TODOS os slots:
// o jogo fica em português com QUALQUER configuração da Steam, sem o usuário mudar nada.

try { Console.OutputEncoding = Encoding.UTF8; } catch { }

// args: --out <arquivo>  (modo teste: grava o bundle e NÃO instala)
//       --data <pasta>   (usa esta FalloutShelter_Data, pula auto-detecção)
string? testOut = GetArg(args, "--out");
string? dataArg = GetArg(args, "--data");
bool interactive = testOut == null;

Banner();

// ----- 1) localizar o jogo -----
string? dataDir = dataArg ?? FindGameData();
if (dataDir == null && interactive) dataDir = PromptForGame();
if (dataDir == null) { Fail("Não encontrei a pasta do Fallout Shelter."); return 1; }

string bundlePath = Path.Combine(dataDir, "data.unity3d");
string managed = Path.Combine(dataDir, "Managed");
string backup = Path.Combine(dataDir, "data.unity3d.bak");

if (!File.Exists(bundlePath) || !Directory.Exists(managed))
{
    Fail($"Pasta inválida (faltou data.unity3d ou Managed):\n  {dataDir}");
    return 1;
}
Console.WriteLine($"Jogo encontrado em:\n  {dataDir}\n");

// ----- 2) elevar se necessário (instalação em Program Files exige admin) -----
if (interactive && !CanWrite(dataDir) && !IsAdmin())
{
    Console.WriteLine("Preciso de permissão de administrador para gravar na pasta do jogo.");
    Console.WriteLine("Vou pedir a confirmação do Windows (UAC)...\n");
    if (RelaunchAsAdmin(new[] { "--data", dataDir }))
        return 0; // a instância elevada continua
    Fail("Não foi possível obter permissão de administrador.");
    return 1;
}

// ----- 3) carregar recursos embutidos -----
var asm = Assembly.GetExecutingAssembly();
var am = new AssetsManager();
using (var tpk = Res(asm, "classdata.tpk"))
{
    var msTpk = new MemoryStream();
    tpk.CopyTo(msTpk); msTpk.Position = 0;
    am.LoadClassPackage(msTpk);
}
Dictionary<string, string> trans;
using (var sr = new StreamReader(Res(asm, "ptbr.csv"), Encoding.UTF8))
    trans = ReadCsvDict(sr);
Console.WriteLine($"Tradução embutida: {trans.Count} textos.");

// ----- 4) abrir o bundle do usuário (em memória, p/ liberar o arquivo) -----
Console.WriteLine("Lendo o data.unity3d... (aguarde, ~458 MB)");
byte[] bundleBytes = File.ReadAllBytes(bundlePath);
var bun = am.LoadBundleFile(new MemoryStream(bundleBytes), bundlePath, true);
var names = bun.file.GetAllFileNames();

// versão do Unity (p/ a class database correta) + coleta de MonoBehaviours
string unityVer = "6000.0.58f2";
var cand = new List<(int dir, AssetsFileInstance afi, AssetFileInfo info)>();
bool verSet = false;
for (int i = 0; i < names.Count; i++)
{
    if (!bun.file.IsAssetsFile(i)) continue;
    AssetsFileInstance afi;
    try { afi = am.LoadAssetsFileFromBundle(bun, i, false); } catch { continue; }
    if (!verSet) { var v = afi.file.Metadata.UnityVersion; if (!string.IsNullOrEmpty(v)) { unityVer = v; verSet = true; } }
    foreach (var info in afi.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        cand.Add((i, afi, info));
}
am.LoadClassDatabaseFromPackage(unityVer);
try { am.MonoTempGenerator = new MonoCecilTempGenerator(managed); } catch { }
cand.Sort((a, b) => b.info.ByteSize.CompareTo(a.info.ByteSize));

// ----- 5) achar o I2 LanguageSource -----
AssetTypeValueField? src = null, srcBase = null;
int srcDir = -1; AssetsFileInstance? srcAfi = null; AssetFileInfo? srcInfo = null;
foreach (var (dir, afi, info) in cand)
{
    AssetTypeValueField bf;
    try { bf = am.GetBaseField(afi, info, AssetReadFlags.None); } catch { continue; }
    if (bf == null) continue;
    var s = bf["mSource"];
    AssetTypeValueField? chosen = null;
    if (!s.IsDummy && !s["mTerms"]["Array"].IsDummy && !s["mLanguages"]["Array"].IsDummy) chosen = s;
    else if (!bf["mTerms"]["Array"].IsDummy && !bf["mLanguages"]["Array"].IsDummy) chosen = bf;
    if (chosen != null) { src = chosen; srcBase = bf; srcDir = dir; srcAfi = afi; srcInfo = info; break; }
}
if (src == null || srcBase == null || srcAfi == null || srcInfo == null)
{
    Fail("Não achei a tabela de textos (I2 LanguageSource). Versão do jogo incompatível?");
    return 1;
}

// ----- 6) injetar pt-BR em TODOS os idiomas (UX: português sem mexer na Steam) -----
var langs = src["mLanguages"]["Array"];
int langCount = langs.Children.Count;
var terms = src["mTerms"]["Array"];
int applied = 0;
foreach (var t in terms.Children)
{
    string key = t["Term"].AsString ?? "";
    if (!trans.TryGetValue(key, out var pt)) continue;
    var slots = t["Languages"]["Array"];
    for (int i = 0; i < slots.Children.Count; i++) slots.Children[i].AsString = pt;
    applied++;
}
Console.WriteLine($"Textos aplicados: {applied} (em todos os {langCount} idiomas).");

// ----- 8) reempacotar (LZ4) -----
Console.WriteLine("Reempacotando o jogo... (pode levar ~1 min)");
srcInfo.SetNewData(srcBase);
bun.file.BlockAndDirInfo.DirectoryInfos[srcDir].SetNewData(srcAfi.file);

string tmpUncomp = Path.Combine(Path.GetTempPath(), "fsptbr_uncomp.tmp");
string lz4Out = testOut ?? Path.Combine(Path.GetTempPath(), "fsptbr_patched.unity3d");
using (var fw = new AssetsFileWriter(tmpUncomp)) bun.file.Write(fw, 0);
var packer = new AssetBundleFile();
using (var rs = File.OpenRead(tmpUncomp))
using (var reader = new AssetsFileReader(rs))
{
    packer.Read(reader);
    using var fw = new AssetsFileWriter(lz4Out);
    packer.Pack(fw, AssetBundleCompressionType.LZ4, false, null);
}
packer.Close();
am.UnloadAll(true);
try { File.Delete(tmpUncomp); } catch { }

// ----- 9) instalar (com backup) -----
if (testOut == null)
{
    if (!File.Exists(backup)) { File.Copy(bundlePath, backup, false); Console.WriteLine("Backup criado: data.unity3d.bak"); }
    File.Copy(lz4Out, bundlePath, true);
    try { File.Delete(lz4Out); } catch { }
    Console.WriteLine();
    Ok("PRONTO! Traducao instalada com sucesso.");
    Console.WriteLine("Abra o jogo - ele ja estara em PORTUGUES (qualquer que seja o idioma da Steam).");
}
else
{
    Console.WriteLine($"[modo teste] bundle gravado em: {lz4Out}");
}

if (interactive) { Console.WriteLine("\nPressione ENTER para sair."); Console.ReadLine(); }
return 0;

// ============================ helpers ============================
static string? GetArg(string[] a, string name)
{
    for (int i = 0; i < a.Length - 1; i++) if (a[i] == name) return a[i + 1];
    return null;
}

static Stream Res(Assembly asm, string logicalName)
    => asm.GetManifestResourceStream(logicalName) ?? throw new Exception("recurso ausente: " + logicalName);

static bool IsAdmin()
{
    try { using var id = WindowsIdentity.GetCurrent(); return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator); }
    catch { return false; }
}

static bool CanWrite(string dir)
{
    try { string t = Path.Combine(dir, ".fsptbr_wtest"); File.WriteAllText(t, "x"); File.Delete(t); return true; }
    catch { return false; }
}

static bool RelaunchAsAdmin(string[] extra)
{
    try
    {
        var exe = Environment.ProcessPath!;
        var psi = new ProcessStartInfo(exe) { UseShellExecute = true, Verb = "runas" };
        foreach (var e in extra) psi.ArgumentList.Add(e);
        Process.Start(psi);
        return true;
    }
    catch { return false; }
}

static string? FindGameData()
{
    var steamRoots = new List<string>
    {
        @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam",
        @"D:\Steam", @"E:\Steam", @"D:\SteamLibrary", @"E:\SteamLibrary"
    };
    var libs = new List<string>();
    foreach (var s in steamRoots)
    {
        libs.Add(s);
        var vdf = Path.Combine(s, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s*\"([^\"]+)\""))
                libs.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
    }
    foreach (var lib in libs.Distinct())
    {
        var data = Path.Combine(lib, "steamapps", "common", "Fallout Shelter", "FalloutShelter_Data");
        if (File.Exists(Path.Combine(data, "data.unity3d"))) return data;
    }
    return null;
}

static string? PromptForGame()
{
    Console.WriteLine("Não achei o jogo automaticamente.");
    Console.WriteLine(@"Cole o caminho da pasta do jogo (ex.: ...\steamapps\common\Fallout Shelter) e tecle ENTER:");
    Console.Write("> ");
    var line = Console.ReadLine()?.Trim().Trim('"');
    if (string.IsNullOrEmpty(line)) return null;
    if (File.Exists(Path.Combine(line, "data.unity3d"))) return line;                      // já é FalloutShelter_Data
    var d = Path.Combine(line, "FalloutShelter_Data");
    if (File.Exists(Path.Combine(d, "data.unity3d"))) return d;
    return null;
}

static void Banner()
{
    Console.WriteLine("=====================================================");
    Console.WriteLine("   Fallout Shelter — Tradução em Português (Brasil)");
    Console.WriteLine("=====================================================\n");
}
static void Ok(string m) { var c = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine(m); Console.ForegroundColor = c; }
static void Fail(string m) { var c = Console.ForegroundColor; Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine("\nERRO: " + m); Console.ForegroundColor = c; Console.WriteLine("\nPressione ENTER para sair."); try { Console.ReadLine(); } catch { } }

static Dictionary<string, string> ReadCsvDict(TextReader r)
{
    var dict = new Dictionary<string, string>();
    var field = new StringBuilder();
    var row = new List<string>();
    bool inQ = false, started = false, headerSkipped = false;
    void EndRow()
    {
        row.Add(field.ToString()); field.Clear();
        if (row.Count >= 2)
        {
            if (!headerSkipped && row[0].Equals("Key", StringComparison.OrdinalIgnoreCase)) headerSkipped = true;
            else dict[row[0]] = row[1];
        }
        row.Clear();
    }
    int ci;
    while ((ci = r.Read()) != -1)
    {
        char ch = (char)ci; started = true;
        if (inQ) { if (ch == '"') { if (r.Peek() == '"') { r.Read(); field.Append('"'); } else inQ = false; } else field.Append(ch); }
        else
        {
            if (ch == '"') inQ = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n') { EndRow(); }
            else field.Append(ch);
        }
    }
    if (started && (field.Length > 0 || row.Count > 0)) EndRow();
    return dict;
}
