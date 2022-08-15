using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : new()
{ 
    protected T instance;
    protected static readonly object Padlock = new();
    protected Singleton() { }

    public T Instance
    { 
        get
        { 
            lock (Padlock)
                return instance ??= new T();
        }
    }
}