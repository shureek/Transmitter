using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace KSoft
{
    /// <summary>
    /// Кэш объеков. Отвечает за кэширование объектов.
    /// </summary>
    /// <remarks>
    /// Фабрика отвечает за кэширование объектов.
    /// Если при вызове конструктора указано useWeakReferences, то находящиеся в кэше объекты могут быть собраны сборщиком мусора.
    /// Класс является потокобезопасным.
    /// </remarks>
    /// <typeparam name="T">Базовый тип хранимых объектов.</typeparam>
    public class ObjectCache<T>
    {
        readonly ConcurrentBag<object> m_bag;
        readonly bool m_useWeakReferences;

        public ObjectCache(bool useWeakReferences)
        {
            m_useWeakReferences = useWeakReferences;
            m_bag = new ConcurrentBag<object>();
        }

        /// <summary>
        /// Получает объект. Если есть свободный объект в кэше, то берет его, иначе возвращает пустое значение.
        /// </summary>
        /// <returns>Требуемый объект.</returns>
        public T Take()
        {
            T obj;
            TryTake(out obj);
            return obj;
        }

        /// <summary>
        /// Получает объект.
        /// </summary>
        /// <param name="obj">Требуемый объект.</param>
        /// <returns><value>true</value>, если объект успешно получен.</returns>
        public bool TryTake(out T obj)
        {
            obj = default(T);
            object temp;
            if (m_bag.TryTake(out temp))
            {
                if (m_useWeakReferences)
                {
                    var wr = temp as WeakReference;
                    if (wr.IsAlive)
                    {
                        obj = (T)wr.Target;
                        return true;
                    }
                }
                else
                {
                    obj = (T)temp;
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Помещает объект в кэш.
        /// </summary>
        /// <param name="obj">Объект.</param>
        public void Put(T obj)
        {
            m_bag.Add(m_useWeakReferences ? (object)new WeakReference(obj) : (object)obj);
        }
    }

    /// <summary>
    /// Фабрика объектов. Отвечает за создание и кэширование группы объектов.
    /// </summary>
    /// <remarks>
    /// В свойстве ObjectType указывается тип объектов, автоматически создаваемых фабрикой.
    /// В дочернем классе нужно переопределить метов CreateObject.
    /// </remarks>
    /// <typeparam name="T">Базовый тип объектов, поставляемых фабрикой.</typeparam>
    public abstract class ObjectFactoryBase<T>
        : IObjectFactory<T>
        where T : class
    {
        readonly ObjectCache<T> m_cache;
#if DEBUG
        /// <summary>
        /// Количество созданных объектов.
        /// </summary>
        volatile int m_createdCount;
#endif
        
        protected ObjectFactoryBase(bool useWeakReferences)
        {
            m_cache = new ObjectCache<T>(useWeakReferences);
        }

        public T Get()
        {
            T obj;
            if (m_cache.TryTake(out obj))
                return obj;
            else
                obj = CreateObject();
            Debug.Assert(obj != null, "Object must not be null", String.Format("Фабрика объектов {0} не смогла получить объект", typeof(T)));
            return obj;
        }

        public void Release(T obj)
        {
            m_cache.Put(obj);
        }

        protected abstract T CreateObject();
    }

    /// <summary>
    /// Фабрика объектов. Отвечает за создание и кэширование группы объектов.
    /// </summary>
    /// <remarks>
    /// В свойстве ObjectType указывается тип объектов, автоматически создаваемых фабрикой.
    /// Объекты создаются конструктором, получаемым из указанного типа.
    /// В коллекцию свойств Properties можно добавить свойства объектов, которые будут инициализированы при создании.
    /// </remarks>
    /// <typeparam name="T">Базовый тип объектов, поставляемых фабрикой.</typeparam>
    public class TypedObjectFactory<T>
        : ObjectFactoryBase<T>
        where T: class
    {
        #region [ Fields and properties ]

        Type m_objectType;
        public Type ObjectType
        {
            get { return m_objectType; }
            set
            {
                if (value == m_objectType)
                    return;
                if (m_objectType != null)
                    throw new InvalidOperationException("Тип уже установлен, его нельзя изменить");
                if (!typeof(T).IsAssignableFrom(value))
                    throw new ArgumentException(String.Format("Type {0} is not assignable from {1}", typeof(T), value), "value");
                m_objectType = value;
            }
        }

        static object[] s_emptyObjects;
        static object[] EmptyObjects
        {
            get
            {
                if (s_emptyObjects == null)
                    s_emptyObjects = new object[0];
                return s_emptyObjects;
            }
        }
        static Type[] s_emptyTypes = new Type[0];
        static Type[] EmptyTypes
        {
            get
            {
                if (s_emptyTypes == null)
                    s_emptyTypes = new Type[0];
                return s_emptyTypes;
            }
        }

        ConstructorInfo m_constructor;
        ConstructorInfo Constructor
        {
            get
            {
                if (m_constructor == null)
                    m_constructor = ObjectType.GetConstructor(EmptyTypes);
                if (m_constructor == null)
                    throw new NullReferenceException("Не найден конструктор без параметров для типа "+ObjectType);
                return m_constructor;
            }
        }

        PropertyCollection m_properties;
        public PropertyCollection Properties
        {
            get
            {
                if (m_properties == null)
                    m_properties = new PropertyCollection(ObjectType);
                return m_properties;
            }
        }

        #endregion

        public TypedObjectFactory(bool useWeakReferences = true)
            : base(useWeakReferences)
        { }

        protected override T CreateObject()
        {
            T obj = (T)Constructor.Invoke(new object[0]);
            foreach (var property in Properties as IEnumerable<KeyValuePair<PropertyInfo, object>>)
                property.Key.SetValue(obj, property.Value, EmptyObjects);
            return obj;
        }
    }

    public class PropertyCollection
        : IDictionary<string, object>, IDictionary<PropertyInfo, object>
    {
        readonly Type m_objectType;
        readonly Dictionary<PropertyInfo, object> m_values;
        readonly Dictionary<string, PropertyInfo> m_properties;

        public PropertyCollection(Type objectType)
        {
            m_objectType = objectType;
            m_properties = new Dictionary<string, PropertyInfo>();
            m_values = new Dictionary<PropertyInfo, object>();
        }

        void Set(string propertyName, object value, bool add)
        {
            PropertyInfo property;
            if (!m_properties.TryGetValue(propertyName, out property))
            {
                property = m_objectType.GetProperty(propertyName);
                if (property == null)
                    throw new ArgumentException("Нет свойства с таким именем", "propertyName");
                m_properties.Add(property.Name, property);
            }

            if (add)
                m_values.Add(property, value);
            else
                m_values[property] = value;
        }

        void Set(PropertyInfo property, object value, bool add)
        {
            if (add)
            {
                m_properties.Add(property.Name, property);
                m_values.Add(property, value);
            }
            else
            {
                m_properties[property.Name] = property;
                m_values[property] = value;
            }
        }

        #region [ IDictionary<string, object> members ]

        public void Add(string propertyName, object value)
        {
            Set(propertyName, value, true);
        }

        public bool ContainsKey(string propertyName)
        {
            return m_properties.ContainsKey(propertyName);
        }

        public ICollection<string> Keys
        {
            get { return m_properties.Keys; }
        }

        public bool Remove(string propertyName)
        {
            PropertyInfo property;
            if (m_properties.TryGetValue(propertyName, out property))
                return Remove(property);
            else
                return false;
        }

        public bool TryGetValue(string propertyName, out object value)
        {
            PropertyInfo property;
            if (m_properties.TryGetValue(propertyName, out property))
                return m_values.TryGetValue(property, out value);
            else
            {
                value = null;
                return false;
            }
        }

        public ICollection<object> Values
        {
            get { return m_values.Values; }
        }

        public object this[string propertyName]
        {
            get
            {
                var property = m_properties[propertyName];
                return m_values[property];
            }
            set { Set(propertyName, value, false); }
        }

        void ICollection<KeyValuePair<string, object>>.Add(KeyValuePair<string, object> item)
        {
            Add(item.Key, item.Value);
        }

        public void Clear()
        {
            m_properties.Clear();
            m_values.Clear();
        }

        bool ICollection<KeyValuePair<string, object>>.Contains(KeyValuePair<string, object> item)
        {
            PropertyInfo property;
            if (m_properties.TryGetValue(item.Key, out property))
                return m_values[property] == item.Value;
            else
                return false;
        }

        void ICollection<KeyValuePair<string, object>>.CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
        {
            int index = arrayIndex;
            foreach (var item in m_values)
            {
                array[index] = new KeyValuePair<string, object>(item.Key.Name, item.Value);
                index++;
            }
        }

        public int Count
        {
            get { return m_values.Count; }
        }

        public bool IsReadOnly
        {
            get { return false; }
        }

        bool ICollection<KeyValuePair<string, object>>.Remove(KeyValuePair<string, object> item)
        {
            PropertyInfo property;
            if (m_properties.TryGetValue(item.Key, out property))
            {
                m_values.Remove(property);
                m_properties.Remove(property.Name);
                return true;
            }
            else
                return false;
        }

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator()
        {
            var array = new KeyValuePair<string, object>[m_properties.Count];
            (this as ICollection<KeyValuePair<string, object>>).CopyTo(array, 0);
            return (array as IEnumerable<KeyValuePair<string, object>>).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return m_values.GetEnumerator();
        }

        #endregion

        #region [ IDictionary<PropertyInfo, object> members ]

        public void Add(PropertyInfo property, object value)
        {
            Set(property, value, true);
        }

        public bool ContainsKey(PropertyInfo property)
        {
            return m_values.ContainsKey(property);
        }

        ICollection<PropertyInfo> IDictionary<PropertyInfo, object>.Keys
        {
            get { return m_values.Keys; }
        }

        public bool Remove(PropertyInfo property)
        {
            bool propertyRemoved = m_properties.Remove(property.Name);
            bool valueRemoved = m_values.Remove(property);
            return propertyRemoved && valueRemoved;
        }

        public bool TryGetValue(PropertyInfo property, out object value)
        {
            return m_values.TryGetValue(property, out value);
        }

        public object this[PropertyInfo property]
        {
            get { return m_values[property]; }
            set { Set(property, value, false); }
        }

        void ICollection<KeyValuePair<PropertyInfo, object>>.Add(KeyValuePair<PropertyInfo, object> item)
        {
            Set(item.Key, item.Value, true);
        }

        bool ICollection<KeyValuePair<PropertyInfo, object>>.Contains(KeyValuePair<PropertyInfo, object> item)
        {
            return (m_values as ICollection<KeyValuePair<PropertyInfo, object>>).Contains(item);
        }

        void ICollection<KeyValuePair<PropertyInfo, object>>.CopyTo(KeyValuePair<PropertyInfo, object>[] array, int arrayIndex)
        {
            (m_values as ICollection<KeyValuePair<PropertyInfo, object>>).CopyTo(array, arrayIndex);
        }

        bool ICollection<KeyValuePair<PropertyInfo, object>>.Remove(KeyValuePair<PropertyInfo, object> item)
        {
            if (!(m_values as ICollection<KeyValuePair<PropertyInfo, object>>).Contains(item))
                return false;
            return Remove(item.Key);
        }

        IEnumerator<KeyValuePair<PropertyInfo, object>> IEnumerable<KeyValuePair<PropertyInfo, object>>.GetEnumerator()
        {
            return m_values.GetEnumerator();
        }

        #endregion
    }

    public class ArrayFactory<T>
        : ObjectFactoryBase<T[]>
    {
        int m_arraySize;
        public int ArraySize
        {
            get { return m_arraySize; }
            set { m_arraySize = value; }
        }

        public ArrayFactory(bool useWeakReferences = true)
            : base(useWeakReferences)
        { }

        protected override T[] CreateObject()
        {
            return new T[ArraySize];
        }
    }
}