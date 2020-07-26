using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace LivingThing.XamarinRazor
{
    public class ParameterComponentGenerator:ComponentGenerator
    {
        static string CodeFormat = @"
using Microsoft.AspNetCore.Components;

namespace {0}
{{
    public partial class {1} : ComponentToParameterBridge<{3}, {1}>
    {{
        public {1}()
        {{
        }}
        public {1}({3} _parameter):base(_parameter)
        {{
        }}
{4}
    }}
}}
";

        protected override string CodeFormatString => CodeFormat;

        public ParameterComponentGenerator(string @namespace, Type elementType, ComponentGenerator parent):base(@namespace, elementType, parent)
        {
        }

    }
}
