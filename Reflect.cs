using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        var asm = Assembly.LoadFrom(@"C:\Users\victo\.nuget\packages\microsoft.aspnetcore.openapi\10.0.10\lib\net10.0\Microsoft.AspNetCore.OpenApi.dll");
        var transformerType = asm.GetTypes().FirstOrDefault(t => t.Name == "IOpenApiOperationTransformer");
        if (transformerType != null)
        {
            var method = transformerType.GetMethod("TransformAsync");
            foreach (var p in method.GetParameters())
            {
                Console.WriteLine($"{p.ParameterType.FullName} {p.Name}");
            }
        }
        else
        {
            Console.WriteLine("IOpenApiOperationTransformer not found.");
        }
        
        var asm2 = Assembly.LoadFrom(@"C:\Users\victo\.nuget\packages\microsoft.openapi\2.0.0\lib\netstandard2.0\Microsoft.OpenApi.dll");
        foreach(var t in asm2.GetTypes().Where(t => t.Name.Contains("OpenApiOperation"))) {
            Console.WriteLine(t.FullName);
        }
    }
}
