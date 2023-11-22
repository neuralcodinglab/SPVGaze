using System;
using System.Collections.Generic;
using UnityEngine;

namespace Xarphos
{
    public static class SingletonRegister
    {
        private static Dictionary<Type, object> _senorsSingletons;

        public static bool RegisterType(object instance)
        {
            _senorsSingletons ??= new Dictionary<Type, object>();

            var type = instance.GetType();
            if (_senorsSingletons.ContainsKey(type))
            {
                Debug.Log($"Tried to register {type}, but is already registered");
                return false;
            }
            _senorsSingletons[type] = instance;

            return true;
        }

        public static bool GetInstance<T>(out T instance)
        {
            if (_senorsSingletons == null || !_senorsSingletons.ContainsKey(typeof(T)))
            {
                Debug.Log($"Failed to retrieve instance of {typeof(T)}");
                instance = default(T);
                return false;
            }
            instance = (T) _senorsSingletons[typeof(T)];
            return true;
        }

        public static T GetInstance<T>()
        {
            GetInstance(out T obj);
            return obj;
        }
    }
}