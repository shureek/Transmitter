using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using System.Reflection;

namespace KSoft
{
    public static class Extensions
    {
        #region [ Transmitter extension ]
        
        public static void LoadSettings(this Transmitter transmitter, string filename)
        {
            var xdoc = XDocument.Load(filename);

            foreach (XElement elt in xdoc.Root.Element("Receivers").Elements("Receiver"))
            {
                IReceiver receiver = (IReceiver)CreateObject(elt);
                if (receiver != null)
                    transmitter.AddReceiver(receiver);
            }

            foreach (XElement elt in xdoc.Root.Element("Senders").Elements("Sender"))
            {
                ISender sender = (ISender)CreateObject(elt);
                if (sender != null)
                    transmitter.AddSender(sender);
            }
        }

        static Dictionary<string, Assembly> s_loadedAssemblies;
        static Assembly[] s_currentAssemblies;

        static Type GetObjectType(XElement element)
        {
            string assemblyName = element.AttributeValue("AssemblyName");
            string typeName = element.AttributeValue("TypeName");

            if (s_loadedAssemblies == null)
            {
                s_loadedAssemblies = new Dictionary<string, Assembly>();
                s_currentAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            }

            Type objType = null;
            if (!String.IsNullOrEmpty(assemblyName))
            {
                // Если сборка указана, то будем искать в этой сборке
                Assembly assembly;
                if (!s_loadedAssemblies.TryGetValue(assemblyName, out assembly))
                {
                    string assemblyPath = System.IO.Path.GetFullPath(assemblyName);
                    assembly = Assembly.LoadFile(assemblyPath);
                }
                objType = assembly.GetType(typeName);
            }
            else
            {
                foreach (Assembly assembly in s_currentAssemblies)
                {
                    objType = assembly.GetType(typeName);
                    if (objType != null)
                        break;
                }
            }
            return objType;
        }

        static bool TypesEqual(Type type1, Type type2)
        {
            if (type1.IsGenericType && !type1.IsGenericTypeDefinition)
                type1 = type1.GetGenericTypeDefinition();
            if (type2.IsGenericType && !type2.IsGenericTypeDefinition)
                type2 = type2.GetGenericTypeDefinition();
            return type1 == type2;
        }

        static Type GetInterface(this Type type, Type interfaceType)
        {
            if (TypesEqual(type, interfaceType))
                return type;
            foreach (var iface in type.GetInterfaces())
            {
                if (TypesEqual(type, iface))
                    return iface;
            }
            return null;
        }

        static object CreateObject(XElement element)
        {
            // Сначала получим экземпляр типа
            var objType = GetObjectType(element);
            object[] noObjects = new object[0];

            // Получаем конструктор и вызываем его
            var constr = objType.GetConstructor(new Type[0]);
            object obj = constr.Invoke(noObjects);

            // Установим свойства
            foreach (XAttribute attr in element.Attributes())
            {
                if (attr.Name == "AssemblyName" || attr.Name == "TypeName")
                    continue;

                var property = objType.GetProperty(attr.Name.LocalName);
                if (property == null)
                    continue;

                object propValue = GetValue(property.PropertyType, attr.Value);
                property.SetValue(obj, propValue, noObjects);
            }

            // Внутренние элементы могут описывать создание внутренних объектов с их свойствами
            foreach (XElement subelement in element.Elements())
            {
                var property = objType.GetProperty(subelement.Name.LocalName);
                if (property != null)
                {
                    object propValue = CreateObject(subelement);
                    property.SetValue(obj, propValue, noObjects);
                    continue;
                }

                // Может, нужно создать фабрику для этого свойства
                property = objType.GetProperty(subelement.Name.LocalName + "Factory");
                if (property != null)
                {
                    // Тип свойства должен реализовывать интерфейс фабрики
                    var factoryInterface = property.PropertyType.GetInterface(typeof(IObjectFactory<>));
                    if (factoryInterface == null)
                        continue;

                    // Получим тип аргумента generic-класса фабрики
                    // IObjectFactory<IMessageReader>
                    var genericArguments = factoryInterface.GetGenericArguments();
                    if (genericArguments.Length != 1)
                        continue;
                    var genericArgument = genericArguments[0];

                    // Получим тип объекта, создаваемого фабрикой
                    var propType = GetObjectType(subelement);

                    if (!genericArgument.IsAssignableFrom(propType))
                        continue;

                    // Создадим фабрику: new TypedObjectFactory<genericArgument>(true)
                    var factoryType = typeof(TypedObjectFactory<>).MakeGenericType(genericArguments);
                    var constructor = factoryType.GetConstructor(new Type[] {typeof(bool)});
                    object factory = constructor.Invoke(new object[] {true});

                    // Получим коллекцию свойств фабрики
                    factoryType.GetProperty("ObjectType").SetValue(factory, propType, noObjects);
                    PropertyCollection objProperties = (PropertyCollection)factoryType.GetProperty("Properties").GetValue(factory, noObjects);
                    
                    // Добавим свойства в коллекцию свойств фабрики
                    foreach (XAttribute attr in subelement.Attributes())
                    {
                        if (attr.Name == "AssemblyName" || attr.Name == "TypeName")
                            continue;

                        var objProperty = propType.GetProperty(attr.Name.LocalName);
                        if (objProperty == null || !objProperty.CanWrite)
                            continue;

                        object propValue = null;
                        bool valueSet = false;
                        try
                        {
                            propValue = GetValue(objProperty.PropertyType, attr.Value);
                            valueSet = true;
                        }
                        catch (Exception ex)
                        {
                            TraceHelper.WriteError(null, new ApplicationException(String.Format("Не удалось преобразовать \"{0}\" в значение типа {1}", attr.Value, objProperty.PropertyType), ex));
                        }
                        if (valueSet)
                            objProperties.Add(objProperty, propValue);
                    }

                    // Установим фабрику объекту
                    property.SetValue(obj, factory, noObjects);
                }
            }

            return obj;
        }

        /// <summary>
        /// Преобразует строку в значение нужного типа.
        /// </summary>
        /// <param name="type">Нужный тип.</param>
        /// <param name="value">Строка для преобразования.</param>
        /// <returns>Значение нужного типа.</returns>
        static object GetValue(Type type, string value)
        {
            if (type == typeof(System.Net.IPAddress))
                return System.Net.IPAddress.Parse(value);

            var converter = System.ComponentModel.TypeDescriptor.GetConverter(type);
            object result = converter.ConvertFromString(value);
            return result;
        }

        #endregion

        #region [ XElement extension ]

        public static string AttributeValue(this XElement element, XName attributeName)
        {
            var attribute = element.Attribute(attributeName);
            return attribute != null ? attribute.Value : null;
        }

        #endregion
    }
}
