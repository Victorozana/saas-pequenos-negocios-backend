$assemblyPath = "C:\Users\victo\.nuget\packages\microsoft.aspnetcore.openapi\10.0.10\lib\net10.0\Microsoft.AspNetCore.OpenApi.dll"
$asm = [Reflection.Assembly]::LoadFrom($assemblyPath)
$type = $asm.GetTypes() | Where-Object Name -eq "IOpenApiOperationTransformer"
$method = $type.GetMethod("TransformAsync")
$method.GetParameters() | ForEach-Object { "$($_.ParameterType.FullName) $($_.Name)" }
