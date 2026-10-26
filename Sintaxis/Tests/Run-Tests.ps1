param([ValidateSet('Debug','Release')][string]$Configuration = 'Release', [string]$RepoRoot = '')
$ErrorActionPreference = 'Stop'
if (!$RepoRoot) { $RepoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }; $repoIntermedio = $RepoRoot
$vswhereIntermedio = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
$msbuildIntermedio = & $vswhereIntermedio -latest -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuildIntermedio) { throw 'No se encontro MSBuild de Visual Studio.' }
$cscIntermedio = Join-Path (Split-Path $msbuildIntermedio) 'Roslyn/csc.exe'
Push-Location $repoIntermedio
try {
    & $msbuildIntermedio Sintaxis/ColinScript.sln /t:Build "/p:Configuration=$Configuration" /p:OutputPath=bin/Intermedio/ /p:IntermediateOutputPath=obj/Intermedio/ /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion.' }
    $rutaBin = (Resolve-Path Sintaxis/ColinScript/bin/Intermedio).Path
    $resolverIntermedio = [System.ResolveEventHandler] {
        param($emisor, $evento)
        $rutaAssembly = Join-Path $rutaBin (([Reflection.AssemblyName]::new($evento.Name).Name) + '.dll')
        if (!(Test-Path -LiteralPath $rutaAssembly)) { $rutaAssembly = [IO.Path]::ChangeExtension($rutaAssembly, '.exe') }
        if (Test-Path -LiteralPath $rutaAssembly) { return [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($rutaAssembly)) }
        return $null
    }
    [AppDomain]::CurrentDomain.add_AssemblyResolve($resolverIntermedio)
    try {
        foreach ($suiteIntermedio in @('CuadruplosTests', 'IntermedioTests')) {
            $pruebaIntermedio = Join-Path $rutaBin ($suiteIntermedio + '.exe')
            & $cscIntermedio /nologo /target:exe "/out:$pruebaIntermedio" "/reference:$rutaBin/ColinScript.exe" /reference:System.Windows.Forms.dll "Sintaxis/Tests/$suiteIntermedio.cs"
            if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion de $suiteIntermedio." }
            $ensambladoPruebas = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($pruebaIntermedio))
            $argumentosPruebas = New-Object object[] 1
            if ($suiteIntermedio -eq 'CuadruplosTests') { $argumentosPruebas[0] = [string[]]@($rutaBin) }
            else { $argumentosPruebas[0] = [string[]]@((Resolve-Path Sintaxis).Path) }
            $resultadoPruebas = $ensambladoPruebas.EntryPoint.Invoke($null, $argumentosPruebas)
            if ($resultadoPruebas -ne 0) { throw "Fallo $suiteIntermedio." }
        }
    }
    finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolverIntermedio) }
}
finally { Pop-Location }

