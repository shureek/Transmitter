using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Xml.Linq;

namespace KSoft
{
    public static class Extensions
    {
        #region [ IList<T> extensions ]
        
        /// <summary>
        /// Возвращает <value>true</value>, если <paramref name="array"/> начинается с <paramref name="template"/>.
        /// </summary>
        /// <param name="array">Исходный список.</param>
        /// <param name="template">Шаблон для сравнения.</param>
        /// <returns><value>true</value>, если <paramref name="array"/> начинается с <paramref name="template"/>.</returns>
        public static bool StartsWith<T>(this IList<T> array, IList<T> template)
        {
            int length = Math.Min(array.Count, template.Count);
            return CompareLists(array, 0, template, 0, length);
        }

        /// <summary>
        /// Возвращает <value>true</value>, если <paramref name="array"/> начинается с одного из <paramref name="templates"/>.
        /// </summary>
        /// <param name="array">Исходный список.</param>
        /// <param name="templates">Шаблоны для сравнения.</param>
        /// <returns><value>true</value>, если <paramref name="array"/> начинается с одного из <paramref name="templates"/>.</returns>
        public static bool StartsWithAny<T>(this IList<T> array, IList<IList<T>> templates)
        {
            foreach (IList<T> template in templates)
                if (array.StartsWith(template))
                    return true;
            return false;
        }

        public static int? FirstOccurenceOf<T>(this IList<T> array, IList<T> template)
        {
            int length = template.Count;
            int maxOffset = array.Count - length;
            for (int offset = 0; offset <= maxOffset; offset++)
                if (CompareLists(array, offset, template, 0, length))
                    return offset;
            return null;
        }

        public static int? FirstOccurenceOfAny<T>(this IList<T> array, IList<IList<T>> templates)
        {
            int minTemplateLength = templates[0].Count;
            for (int index = 1; index < templates.Count; index++)
                if (templates[index].Count < minTemplateLength)
                    minTemplateLength = templates[index].Count;

            for (int offset = 0; offset < array.Count - minTemplateLength + 1; offset++)
            {
                foreach (IList<T> template in templates)
                {
                    if (template.Count < array.Count - offset)
                        continue;
                    if (CompareLists(array, offset, template, 0, template.Count))
                        return offset;
                }
            }

            return null;
        }

        static bool CompareLists<T>(IList<T> list1, int offset1, IList<T> list2, int offset2, int length)
        {
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            for (int index = 0; index < length; index++)
                if (!comparer.Equals(list1[offset1 + index], list2[offset2 + index]))
                    return false;
            return true;
        }

        public static IList<T> GetSegment<T>(this IList<T> list, int offset, int count)
        {
            if (list is ArraySegment<T>)
            {
                var segment = (ArraySegment<T>)list;
                return new ArraySegment<T>(segment.Array, segment.Offset + offset, count);
            }
            else if (list is T[])
            {
                return new ArraySegment<T>(list as T[], offset, count);
            }
            else
            {
                var array = new T[count];
                for (int i = 0; i < count; i++)
                    array[i] = list[offset + i];
                return array;
            }
        }

        public static IList<T> GetSegment<T>(this IList<T> list, int offset)
        {
            return list.GetSegment(offset, list.Count - offset);
        }

        public static IList<ArraySegment<T>> ToBufferList<T>(this IList<T> list)
        {
            if (list is T[])
            {
                return new ArraySegment<T>[] {new ArraySegment<T>(list as T[], 0, list.Count)};
            }
            else if (list is ArraySegment<T>)
            {
                return new ArraySegment<T>[] {(ArraySegment<T>)list};
            }
            else
            {
                var array = new T[list.Count];
                list.CopyTo(array, 0);
                return new ArraySegment<T>[] {new ArraySegment<T>(array, 0, list.Count)};
            }
        }

        #endregion

        #region [ IList<byte> extension ]

        public static string ToHexString(this IList<byte> bytes)
        {
            var sb = new System.Text.StringBuilder(bytes.Count * 3 - 1);
            for (int i = 0; i < bytes.Count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(bytes[i].ToString("x2"));
            }
            return sb.ToString();
        }

        #endregion

        #region [ IList<string> extension ]

        public static string ToSeparatedString(this IList<string> strings, string separator)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string str in strings)
            {
                if (sb.Length > 0)
                    sb.Append(separator);
                sb.Append(str);
            }
            return sb.ToString();
        }

        public static string ToSeparatedString(this IList<string> strings)
        {
            return strings.ToSeparatedString(Environment.NewLine);
        }

        #endregion

        #region [ Encoding extension ]

        /// <summary>
        /// Decodes specified bytes into a string.
        /// </summary>
        /// <param name="encoding">Encoding.</param>
        /// <param name="bytes">Bytes to decode from.</param>
        /// <param name="index">Index of first byte to decode.</param>
        /// <param name="count">Number of bytes to decode.</param>
        /// <returns>Result string.</returns>
        public static string GetString(this System.Text.Encoding encoding, IList<byte> bytes, int index, int count)
        {
            var buffers = bytes.ToBufferList();
            if (buffers.Count == 1)
            {
                var buffer = buffers[0];
                return encoding.GetString(buffer.Array, buffer.Offset + index, count);
            }
            else
            {
                throw new NotImplementedException();
                /*System.Text.StringBuilder sb = new StringBuilder();
                foreach (var buffer in buffers)
                    sb.Append(encoding.GetString(buffer.Array, buffer.Offset, buffer.Count));
                return sb.ToString();*/
            }
        }

        public static string GetString(this System.Text.Encoding encoding, IList<byte> bytes)
        {
            return encoding.GetString(bytes, 0, bytes.Count);
        }

        #endregion

        #region [ StringBuilder extension ]

        public static void AppendNotEmpty(this System.Text.StringBuilder sb, string format, object value, string separator)
        {
            if (ValueIsEmpty(value))
                return;
            if (sb.Length > 0)
                sb.Append(separator);
            sb.AppendFormat(format, value);
        }

        static bool ValueIsEmpty(object value)
        {
            if (value == null)
                return true;
            if (value is String)
                return String.IsNullOrEmpty(value as String);
            if (value is DateTime)
                return false;
            if (value is int)
                return (int)value == 0;
            return false;
        }

        #endregion

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
                    var constructor = factoryType.GetConstructor(new Type[] { typeof(bool) });
                    object factory = constructor.Invoke(new object[] { true });

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