using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Input;
using Xamarin.Forms;

namespace LivingThing.XamarinRazor
{
    public class ComponentGenerator
    {
        static string CodeFormat = @"
using Microsoft.AspNetCore.Components;
using LivingThing.Core.Frameworks.XamarinRazor.Forms;

namespace {0}
{{
    public partial class {1} : {2}<{3}, {1}>
    {{
        public {1}()
        {{
        }}
        public {1}({3} _element):base(_element)
        {{
        }}

{4}
    }}
}}
";

        public ComponentGenerator(string classNamespace, Type elementType, ComponentGenerator parent = null)
        {
            Namespace = classNamespace;
            Parent = parent;
            ElementType = elementType;
        }

        public string Namespace { get; }
        public Type ElementType { get; }
        ComponentGenerator Parent { get; }

        protected int Depth
        {
            get
            {
                int d = 0;
                ComponentGenerator par = Parent;
                while (par != null)
                {
                    d++;
                    par = par.Parent;
                }
                return d;
            }
        }

        protected string Tabs
        {
            get
            {
                string tabs = "";
                int d = Depth;
                while (d-- > 0)
                {
                    tabs += "\t";
                }
                return tabs;
            }
        }
        protected static string GetTypeName(Type type)
        {
            if (type.IsGenericType)
            {
                return $"{type.FullName.Split(new char[] { '`' })[0]}<{string.Join(", ", type.GetGenericArguments().Select(arg => GetTypeName(arg)))}>";
            }
            return type.FullName.Replace("+", ".");
        }

        static string[] cSharpReservedNames = new string[] { "class" };
        protected static string GetProperyName(string name)
        {
            if (cSharpReservedNames.Contains(name))
            {
                return "@" + name;
            }
            return name;
        }

        protected virtual string CodeFormatString => CodeFormat;

        protected virtual string GenerateItem(string propertyType, PropertyInfo property)
        {
            return $"\t\t[Parameter] public {propertyType} this[{string.Join(", ", property.GetIndexParameters().Select(p => $"{GetTypeName(p.ParameterType)} {p.Name}"))}] {{ set => P[{string.Join(", ", property.GetIndexParameters().Select(p => p.Name))}] = value; get => P[{string.Join(", ", property.GetIndexParameters().Select(p => p.Name))}]; }}";
        }

        protected virtual string GenerateProperty(string propertyType, PropertyInfo property)
        {
            return $"\t\t[Parameter] public {propertyType} {GetProperyName(property.Name)} {{ set => P.{GetProperyName(property.Name)} = value; get => P.{GetProperyName(property.Name)}; }}";
        }

        protected virtual string GenerateEventHandler(MethodInfo method)
        {
            var parameter = method.GetParameters()[0];
            var genericArgs = parameter.ParameterType.GetGenericArguments();
            var genericArg = genericArgs[genericArgs.Length - 1];
            string fieldType = GetTypeName(genericArg);
            //string fieldType = GetTypeName(parameter.ParameterType);
            return $"\t\t[Parameter] public System.EventHandler<{fieldType}> {method.Name.Replace("add_", "")} {{ set => P.{method.Name.Replace("add_", "")} += value; }}";
        }

        protected virtual string GenerateEventCallbackFromEventHandler(MethodInfo method)
        {
            var parameter = method.GetParameters()[0];
            var genericArgs = parameter.ParameterType.GetGenericArguments();
            var genericArg = genericArgs[genericArgs.Length - 1];
            string fieldType = GetTypeName(genericArg);
            string methodName = method.Name.Replace("add_", "");
            return $"\t\tEventCallback<{fieldType}> _on{methodName};\r\n" +
                    $"\t\t[Parameter] public EventCallback<{fieldType}> On{methodName} {{ set {{ if (!_on{methodName}.HasDelegate) {{ P.{methodName} += (s, e) => _on{methodName}.InvokeAsync(e); }} _on{methodName} = value; }} }}";
//            return $"\t\t[Parameter] public EventCallback<{fieldType}> On{method.Name.Replace("add_", "")} {{ set => P.{method.Name.Replace("add_", "")} += (s, e) => value.InvokeAsync(e); }}";
        }

        protected virtual string GenerateEventCallback(PropertyInfo property)
        {
            return $"\t\t[Parameter] public EventCallback On{property.Name} {{ set {{ {property.Name} = new Xamarin.Forms.Command(async () => {{ await value.InvokeAsync(this); }}); }} }}";
        }

        protected virtual string GenerateBindableProperty(FieldInfo field)
        {
            var name = field.Name.Replace("Property", "");
            return $"\t\t[Parameter] public Xamarin.Forms.Binding Bind{name} {{ set {{ P.SetBinding({ElementType.FullName}.{field.Name}, value); }} }}";
        }

        public string Generate(string outputPath)
        {
            List<Type> innerGenerateComponents = new List<Type>();
            var properties = string.Join("\r\n" + Tabs, ElementType.GetMembers(BindingFlags.Instance | BindingFlags.Public)
                .Where(member =>
                {
                    if (member is PropertyInfo p)
                    {
                        return p.CanWrite && p.CanRead && (p.GetSetMethod()?.IsPublic ?? false) && p.GetCustomAttribute<ObsoleteAttribute>() == null;
                    }
                    if (member is MethodInfo method)
                    {
                        var parameters = method.GetParameters();
                        return parameters.Length == 1 && (parameters[0].ParameterType is EventHandler || (parameters[0].ParameterType.IsGenericType && parameters[0].ParameterType.GetGenericTypeDefinition() == typeof(EventHandler<>)));
                    }
                    return false;
                })
                .SelectMany<MemberInfo, string>(member =>
                {
                    List<string> returns = new List<string>();
                    if (member is PropertyInfo property)
                    {
                        string propertyType = GetTypeName(property.PropertyType);
                        if (!typeof(Element).IsAssignableFrom(property.PropertyType) && property.PropertyType.IsClass && !property.PropertyType.IsPrimitive && property.PropertyType.GetConstructor(new Type[0]) != null && property.PropertyType != typeof(object))
                        {
                            //if (!innerGenerateComponents.Contains(property.PropertyType))
                            //{
                            //    innerGenerateComponents.Add(property.PropertyType);
                            //    var generator = new InternalComponentGenerator(property.PropertyType, this);
                            //    returns.Add(generator.Generate());
                            //}
                            if (!innerGenerateComponents.Contains(property.PropertyType))
                            {
                                innerGenerateComponents.Add(property.PropertyType);
                                var generator = new ParameterComponentGenerator(Namespace, property.PropertyType, null);
                                generator.Generate(outputPath);
                            }
                            //propertyType = propertyType.Split(new char[] { '.' }).Last()+"Parameter";
                        }
                        if (property.Name == "Item")
                        {
                            returns.Add(GenerateItem(propertyType, property));
                        }
                        else
                        {
                            returns.Add(GenerateProperty(propertyType, property));
                            FieldInfo bindable;
                            if ((bindable = ElementType.GetField(property.Name + "Property", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)) != null)
                            {
                                returns.Add(GenerateBindableProperty(bindable));
                            }
                        }
                        if (property.PropertyType == typeof(ICommand))
                        {
                            returns.Add(GenerateEventCallback(property));
                        }
                    }
                    if (member is MethodInfo method)
                    {
                        if (method.Name.StartsWith("add_"))
                        {
                            returns.Add(GenerateEventHandler(method));
                            returns.Add(GenerateEventCallbackFromEventHandler(method));
                        }
                    }
                    return returns;
                }));
            string baseClass = "ComponentToXamarinBridge";
            if (typeof(Layout).IsAssignableFrom(ElementType))
            {
                baseClass = "ComponentToXamarinLayoutBridge";
            }
            var code = string.Format(CodeFormatString, Namespace, ElementType.Name, baseClass, ElementType.FullName, properties);
            if (outputPath != null)
            {
                File.WriteAllText(outputPath + "/" + ElementType.Name + ".cs", code);
            }
            return code;
        }
    }
}
