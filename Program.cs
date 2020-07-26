using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xamarin.Forms;

namespace LivingThing.XamarinRazor
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<string, string> commands = new Dictionary<string, string>();
            for (int i = 0; i < args.Length;)
            {
                var key = args[i++];
                string value = "";
                if (key.StartsWith("--"))
                {
                    value = args[i++];
                }
                commands[key] = value.Trim(new char[] { '"' });
            }
            if (commands.ContainsKey("help"))
            {
                Console.WriteLine(@"
    LivingThing.Tools.XamarinRazor [Options]

    Options
    --namespace         Namespace of generated classes. Defaults to LivingThing.Core.Frameworks.XamarinRazor.Forms
    --output            Output Path. Default to [CurrentDirectory]/XamarinRazor
    --assemblies        Paths to dll file to generate from (separate multiple files by ;). Defaults to Xamarin.Forms.dll
");
            }
            else
            {
                string @namespace = null;
                if (!commands.TryGetValue("--namespace", out @namespace))
                {
                    @namespace = "LivingThing.Core.Frameworks.XamarinRazor.Forms";
                }
                string outputPath = null;
                if (!commands.TryGetValue("--output", out outputPath))
                {
                    outputPath = Directory.GetCurrentDirectory() + "/XamarinRazor"; //@"E:\Apps\LivingThing\Libraries\LivingThing.Frameworks\LivingThing.Core.Frameworks.Xamarin.Razor\Forms";
                }
                outputPath = @"E:\Apps\LivingThing\Libraries\LivingThing.Frameworks\LivingThing.Core.Frameworks.XamarinRazor\Forms";
                string extraAssemblies = null;
                commands.TryGetValue("--assemblies", out extraAssemblies);
                List<Assembly> assemblies = new List<Assembly>();
                if (extraAssemblies != null)
                {
                    var assembyNames = extraAssemblies.Split(new char[] { ';' });
                    foreach(var assembyName in assembyNames)
                    {
                        assemblies.Add(Assembly.LoadFile(assembyName));
                    }
                }
                else
                {
                    assemblies.Add(typeof(Button).Assembly);
                    //assemblies.Add(typeof(ColorPicker.AlphaSlider).Assembly);
                    //assemblies.Add(typeof(SkiaSharp.Views.Forms.SKCanvasView).Assembly);
                }
                var componentGenerators = assemblies.SelectMany(a => 
                        a.ExportedTypes.Where(t => !t.IsAbstract && t.IsPublic && typeof(Element).IsAssignableFrom(t) && t.GetConstructor(new Type[] { }) != null)
                    )
                    .Select(t => new ComponentGenerator(@namespace, t)).ToArray();
                if (!Directory.Exists(outputPath))
                {
                    Directory.CreateDirectory(outputPath);
                }
                Console.WriteLine($"Writing {componentGenerators.Length} classes to {outputPath}");
                foreach (var generator in componentGenerators)
                {
                    Console.WriteLine($"Writing {generator.ElementType.FullName}");
                    string code = generator.Generate(outputPath + "/");
                    //File.WriteAllText(outputPath + "/" + generator.ElementType.Name + ".cs", code);
                }
            }
        }
    }
}
