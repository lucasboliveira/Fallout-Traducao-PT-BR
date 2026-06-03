using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// FSI2Inject — injeta traduções pt-BR numa coluna alvo do I2 LanguageSource dentro de data.unity3d e repacka (LZ4).
// Uso: FSI2Inject <traducoes.csv> <saidaBundle> [targetIdx=5] [inBundle] [tpk] [managed]
//   traducoes.csv : CSV com cabeçalho. Colunas: Key,<texto>. (usa a 1a e a 2a coluna)

if (args.Length < 2)
{
    Console.WriteLine("Uso: FSI2Inject <traducoes.csv> <saidaBundle> [targetIdx=5] [inBundle] [tpk] [managed]");
    return 2;
}
string transCsv = args[0];
string outBundle = args[1];
int targetIdx = args.Length > 2 ? int.Parse(args[2]) : 5;
string inBundle = args.Length > 3 ? args[3] : @"C:\Program Files (x86)\Steam\steamapps\common\Fallout Shelter\FalloutShelter_Data\data.unity3d";
string tpk = args.Length > 4 ? args[4] : @"C:\Users\wwwlu\Downloads\FSptBR\classdata.tpk";
string managed = args.Length > 5 ? args[5] : @"C:\Program Files (x86)\Steam\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed";

try { Console.OutputEncoding = Encoding.UTF8; } catch { }

// --- carregar traduções ---
var trans = new Dictionary<string, string>();
foreach (var row in ReadCsv(transCsv))
{
    if (row.Length < 2) continue;
    if (trans.Count == 0 && row[0].Equals("Key", StringComparison.OrdinalIgnoreCase)) continue; // pula cabeçalho
    trans[row[0]] = row[1];
}
Console.WriteLine($"Traduções carregadas: {trans.Count}");
if (trans.Count == 0) { Console.WriteLine("Nada para injetar."); return 1; }

// --- abrir bundle ---
var am = new AssetsManager();
am.LoadClassPackage(tpk);
am.LoadClassDatabaseFromPackage("6000.0.58f2");
try { am.MonoTempGenerator = new MonoCecilTempGenerator(managed); } catch (Exception e) { Console.WriteLine($"WARN MonoCecil: {e.Message}"); }
Console.WriteLine($"managed='{managed}' exists={Directory.Exists(managed)} asmcs={File.Exists(Path.Combine(managed, "Assembly-CSharp.dll"))} | MonoTempGenerator set={am.MonoTempGenerator != null}");

Console.WriteLine($"Abrindo bundle: {inBundle}");
var bun = am.LoadBundleFile(inBundle, true);
var names = bun.file.GetAllFileNames();

var cand = new List<(int dir, AssetsFileInstance afi, AssetFileInfo info)>();
for (int i = 0; i < names.Count; i++)
{
    if (!bun.file.IsAssetsFile(i)) continue;
    AssetsFileInstance afi;
    try { afi = am.LoadAssetsFileFromBundle(bun, i, false); } catch { continue; }
    foreach (var info in afi.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        cand.Add((i, afi, info));
}
cand.Sort((a, b) => b.info.ByteSize.CompareTo(a.info.ByteSize));
Console.WriteLine($"Candidatos MB: {cand.Count}, maior size={(cand.Count > 0 ? cand[0].info.ByteSize : 0)}");

int srcDir = -1; AssetsFileInstance? srcAfi = null; AssetFileInfo? srcInfo = null;
AssetTypeValueField? src = null;
AssetTypeValueField? srcBase = null;
int scanned = 0;
foreach (var (dir, afi, info) in cand)
{
    scanned++;
    AssetTypeValueField bf;
    try { bf = am.GetBaseField(afi, info, AssetReadFlags.None); }
    catch (Exception e) { if (scanned <= 8) Console.WriteLine($"  [{scanned}] size={info.ByteSize} deser FALHOU: {e.Message}"); continue; }
    if (bf == null) { if (scanned <= 8) Console.WriteLine($"  [{scanned}] size={info.ByteSize} bf=null"); continue; }
    var s = bf["mSource"];
    if (scanned == 1) Console.WriteLine("    campos base: " + string.Join(", ", bf.Children.Select(c => c.FieldName)));
    if (scanned <= 8) Console.WriteLine($"  [{scanned}] size={info.ByteSize} mSource.IsDummy={s.IsDummy}");
    AssetTypeValueField? chosen = null;
    if (!s.IsDummy && !s["mTerms"]["Array"].IsDummy && !s["mLanguages"]["Array"].IsDummy) chosen = s;   // LanguageSourceAsset.mSource
    else if (!bf["mTerms"]["Array"].IsDummy && !bf["mLanguages"]["Array"].IsDummy) chosen = bf;          // LanguageSource (campos no topo)
    if (chosen != null) { src = chosen; srcBase = bf; srcDir = dir; srcAfi = afi; srcInfo = info; break; }
}
if (src == null || srcBase == null || srcAfi == null || srcInfo == null)
{
    Console.WriteLine("ERRO: LanguageSource não encontrado."); return 1;
}
Console.WriteLine($"LanguageSource em '{names[srcDir]}' pathId={srcInfo.PathId}");

// confere idioma alvo
var langArr = src["mLanguages"]["Array"];
if (targetIdx < 0 || targetIdx >= langArr.Children.Count) { Console.WriteLine($"ERRO: targetIdx {targetIdx} fora de [0,{langArr.Children.Count - 1}]"); return 1; }
Console.WriteLine($"Coluna alvo [{targetIdx}] = '{langArr.Children[targetIdx]["Name"].AsString}' (code='{langArr.Children[targetIdx]["Code"].AsString}')");

// --- aplicar traduções ---
var terms = src["mTerms"]["Array"];
int applied = 0, skippedNoSlot = 0, notFound = 0;
foreach (var t in terms.Children)
{
    string key = t["Term"].AsString ?? "";
    if (!trans.TryGetValue(key, out var ptbr)) { notFound++; continue; }
    var slots = t["Languages"]["Array"];
    if (targetIdx >= slots.Children.Count) { skippedNoSlot++; continue; }
    slots.Children[targetIdx].AsString = ptbr;
    applied++;
}
Console.WriteLine($"Aplicadas: {applied} | termos sem tradução no CSV: {notFound} | sem slot alvo: {skippedNoSlot}");
int unmatched = trans.Count - applied;
if (unmatched > 0) Console.WriteLine($"AVISO: {unmatched} chaves do CSV não casaram com nenhum termo do jogo.");

// --- gravar de volta ---
srcInfo.SetNewData(srcBase);
bun.file.BlockAndDirInfo.DirectoryInfos[srcDir].SetNewData(srcAfi.file);

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outBundle))!);
string tmp = outBundle + ".uncomp.tmp";
Console.WriteLine("Escrevendo bundle não-comprimido (temporário)...");
using (var fw = new AssetsFileWriter(tmp)) { bun.file.Write(fw, 0); }

Console.WriteLine("Comprimindo (LZ4) para a saída final...");
var packer = new AssetBundleFile();
using (var rs = File.OpenRead(tmp))
using (var reader = new AssetsFileReader(rs))
{
    packer.Read(reader);
    using var fw = new AssetsFileWriter(outBundle);
    packer.Pack(fw, AssetBundleCompressionType.LZ4, false, null);
}
packer.Close();
try { File.Delete(tmp); } catch { }

var outLen = new FileInfo(outBundle).Length;
Console.WriteLine($"OK -> {outBundle} ({outLen / 1024 / 1024} MB), {applied} termos injetados na coluna [{targetIdx}].");
return 0;

static List<string[]> ReadCsv(string path)
{
    var rows = new List<string[]>();
    using var r = new StreamReader(path, Encoding.UTF8, true);
    var field = new StringBuilder();
    var row = new List<string>();
    bool inQ = false; bool started = false;
    int ci;
    while ((ci = r.Read()) != -1)
    {
        char ch = (char)ci; started = true;
        if (inQ)
        {
            if (ch == '"') { if (r.Peek() == '"') { r.Read(); field.Append('"'); } else inQ = false; }
            else field.Append(ch);
        }
        else
        {
            if (ch == '"') inQ = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\r') { }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row.ToArray()); row = new List<string>(); }
            else field.Append(ch);
        }
    }
    if (started && (field.Length > 0 || row.Count > 0)) { row.Add(field.ToString()); rows.Add(row.ToArray()); }
    return rows;
}
