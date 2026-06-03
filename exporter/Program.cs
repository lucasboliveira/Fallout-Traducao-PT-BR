using System.Text;
using AssetsTools.NET;
using AssetsTools.NET.Extra;

// FSI2Export — extrai os termos do I2 LanguageSource de dentro do data.unity3d para CSV.
// Uso: FSI2Export [saida.csv] [caminho_FalloutShelter_Data]

string bundlePath = args.Length > 1
    ? args[1]
    : @"C:\Program Files (x86)\Steam\steamapps\common\Fallout Shelter\FalloutShelter_Data\data.unity3d";
string managed = args.Length > 2
    ? args[2]
    : @"C:\Program Files (x86)\Steam\steamapps\common\Fallout Shelter\FalloutShelter_Data\Managed";
string outCsv = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "fs_terms.csv");

try { Console.OutputEncoding = Encoding.UTF8; } catch { }
Console.WriteLine($"Bundle : {bundlePath}");
Console.WriteLine($"Managed: {managed}");

var am = new AssetsManager();

string tpk = args.Length > 3 ? args[3] : Path.Combine(AppContext.BaseDirectory, "classdata.tpk");
if (!File.Exists(tpk)) tpk = @"C:\Users\wwwlu\Downloads\FSptBR\classdata.tpk";
am.LoadClassPackage(tpk);
am.LoadClassDatabaseFromPackage("6000.0.58f2");
Console.WriteLine($"ClassDatabase: {tpk} (alvo 6000.0.58f2)");

try
{
    am.MonoTempGenerator = new MonoCecilTempGenerator(managed);
    Console.WriteLine("MonoCecil temp generator: OK (fallback se o type-tree não estiver embutido).");
}
catch (Exception e) { Console.WriteLine($"WARN MonoCecil: {e.Message}"); }

Console.WriteLine("Carregando + descomprimindo o bundle na memória (pode demorar alguns segundos)...");
var bun = am.LoadBundleFile(bundlePath, true);
var names = bun.file.GetAllFileNames();
Console.WriteLine($"Entradas internas no bundle: {names.Count}");

var mbs = new List<(string file, AssetsFileInstance afi, AssetFileInfo info)>();
bool ttPrinted = false;
for (int i = 0; i < names.Count; i++)
{
    if (!bun.file.IsAssetsFile(i)) continue;
    AssetsFileInstance afi;
    try { afi = am.LoadAssetsFileFromBundle(bun, i, false); }
    catch (Exception e) { Console.WriteLine($"  [{i}] {names[i]}: falha ao carregar: {e.Message}"); continue; }

    if (!ttPrinted)
    {
        Console.WriteLine($"  Unity {afi.file.Metadata.UnityVersion} | TypeTreeEnabled={afi.file.Metadata.TypeTreeEnabled}");
        ttPrinted = true;
    }
    foreach (var info in afi.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        mbs.Add((names[i], afi, info));
}
Console.WriteLine($"MonoBehaviours totais: {mbs.Count}. Procurando o I2 LanguageSource (maiores primeiro)...");
mbs.Sort((a, b) => b.info.ByteSize.CompareTo(a.info.ByteSize));

AssetTypeValueField? src = null;
string srcFile = "";
long srcPath = 0;
int scanned = 0;
foreach (var (file, afi, info) in mbs)
{
    scanned++;
    AssetTypeValueField bf;
    try { bf = am.GetBaseField(afi, info, AssetReadFlags.None); }
    catch (Exception e) { if (scanned <= 8) Console.WriteLine($"  deser falhou (size={info.ByteSize}): {e.Message}"); continue; }
    if (bf == null) continue;

    var s = FindSource(bf);
    if (s != null)
    {
        src = s; srcFile = file; srcPath = info.PathId;
        Console.WriteLine($"ACHEI o I2 source em '{file}' pathId={info.PathId} size={info.ByteSize} (após {scanned} MBs).");
        break;
    }
}
if (src == null) { Console.WriteLine("ERRO: nenhum I2 LanguageSource encontrado."); return 1; }

// --- idiomas ---
var langArr = src["mLanguages"]["Array"];
var langNames = new List<string>();
Console.WriteLine($"Idiomas ({langArr.Children.Count}):");
for (int i = 0; i < langArr.Children.Count; i++)
{
    var L = langArr.Children[i];
    string nm = L["Name"].AsString ?? "";
    string code = L["Code"].AsString ?? "";
    langNames.Add(nm);
    Console.WriteLine($"  [{i}] {nm}  ({code})");
}

// --- termos ---
var termArr = src["mTerms"]["Array"];
Console.WriteLine($"Termos: {termArr.Children.Count}");
if (termArr.Children.Count > 0)
{
    var t0 = termArr.Children[0];
    Console.WriteLine("Campos do 1o termo: " + string.Join(", ", t0.Children.Select(c => $"{c.FieldName}:{c.TypeName}")));
    Console.WriteLine($"Chave do 1o termo: {t0["Term"].AsString}");
}

string fullOut = Path.GetFullPath(outCsv);
Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
using (var sw = new StreamWriter(fullOut, false, new UTF8Encoding(true)))
{
    var sb = new StringBuilder();
    sb.Append("Key");
    foreach (var ln in langNames) { sb.Append(','); sb.Append(Csv(ln)); }
    sw.WriteLine(sb.ToString());

    foreach (var t in termArr.Children)
    {
        sb.Clear();
        sb.Append(Csv(t["Term"].AsString));
        var tl = t["Languages"]["Array"];
        for (int i = 0; i < langNames.Count; i++)
        {
            string v = i < tl.Children.Count ? (tl.Children[i].AsString ?? "") : "";
            sb.Append(','); sb.Append(Csv(v));
        }
        sw.WriteLine(sb.ToString());
    }
}
Console.WriteLine($"CSV gravado: {fullOut}");
Console.WriteLine($"RESUMO -> file='{srcFile}' pathId={srcPath} idiomas={langNames.Count} termos={termArr.Children.Count}");
return 0;

static AssetTypeValueField? FindSource(AssetTypeValueField bf)
{
    var s = bf["mSource"];
    if (!s.IsDummy && !s["mTerms"]["Array"].IsDummy && !s["mLanguages"]["Array"].IsDummy) return s;
    if (!bf["mTerms"]["Array"].IsDummy && !bf["mLanguages"]["Array"].IsDummy) return bf;
    return null;
}

static string Csv(string? s)
{
    s ??= "";
    if (s.IndexOfAny(new[] { '"', ',', '\n', '\r' }) >= 0)
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    return s;
}
