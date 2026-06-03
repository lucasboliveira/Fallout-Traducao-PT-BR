<#
  Instalador da tradução pt-BR do Fallout Shelter (método: edição de assets).
  Faz backup do data.unity3d original e instala a versão traduzida.

  COMO USAR:
    1. Feche o jogo e a Steam.
    2. Clique com o botão direito neste arquivo > "Executar com o PowerShell" como ADMINISTRADOR
       (ou abra um PowerShell como administrador e rode:  .\instalar_traducao.ps1 )
    3. No jogo, defina o idioma como RUSSO (a tradução ocupa o slot do russo).
       Steam > Fallout Shelter > Propriedades > Idioma > Russo   (ou no menu do jogo)

  Para DESINSTALAR: rode  .\instalar_traducao.ps1 -Restaurar
#>
param(
  [string]$Game = "C:\Program Files (x86)\Steam\steamapps\common\Fallout Shelter",
  [string]$Patched = "$PSScriptRoot\work\data.unity3d.patched",
  [switch]$Restaurar
)

$ErrorActionPreference = "Stop"
$dataDir = Join-Path $Game "FalloutShelter_Data"
$target  = Join-Path $dataDir "data.unity3d"
$backup  = Join-Path $dataDir "data.unity3d.bak"

if(-not (Test-Path $target)){ Write-Host "ERRO: não achei $target. Ajuste -Game." -ForegroundColor Red; exit 1 }

if($Restaurar){
  if(Test-Path $backup){
    Copy-Item $backup $target -Force
    Write-Host "Restaurado o data.unity3d original a partir do backup." -ForegroundColor Green
  } else {
    Write-Host "Sem backup (.bak). Use a Steam: Propriedades > Arquivos > Verificar integridade." -ForegroundColor Yellow
  }
  exit 0
}

if(-not (Test-Path $Patched)){ Write-Host "ERRO: não achei o arquivo traduzido em $Patched" -ForegroundColor Red; exit 1 }

# Backup do original (apenas uma vez)
if(-not (Test-Path $backup)){
  Copy-Item $target $backup -Force
  Write-Host "Backup criado: $backup" -ForegroundColor Green
} else {
  Write-Host "Backup já existe (mantido): $backup" -ForegroundColor DarkGray
}

# Tenta detectar escrita sem permissão (Program Files exige admin)
try {
  Copy-Item $Patched $target -Force
} catch {
  Write-Host "FALHA ao escrever em $target." -ForegroundColor Red
  Write-Host "Rode este script como ADMINISTRADOR (a pasta fica em Program Files)." -ForegroundColor Yellow
  exit 1
}

$mb = [math]::Round((Get-Item $target).Length/1MB,1)
Write-Host "OK! Tradução instalada ($mb MB)." -ForegroundColor Green
Write-Host "No jogo/Steam, selecione o idioma RUSSO para ver o português." -ForegroundColor Cyan
