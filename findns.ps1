$assemblyPath = "C:\Users\victo\.nuget\packages\microsoft.openapi\2.0.0\lib\netstandard2.0\Microsoft.OpenApi.dll"
$asm = [Reflection.Assembly]::LoadFrom($assemblyPath)
$types = $asm.GetTypes() | Where-Object { $_.Name -like "*OpenApiDocument*" }
foreach ($t in $types) {
    Write-Output ($t.FullName)
}
