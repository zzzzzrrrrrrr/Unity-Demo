// Path: Assets/_Scripts/Tools/EventCenter.cs
using System;
using System.Collections.Generic;

namespace ARPGDemo.Tools
{
    /// <summary>
    /// </summary>
    public static class EventCenter
    {
        private static readonly Dictionary<Type, Delegate> EventTable = new Dictionary<Type, Delegate>(64);

        /// <summary>
        /// </summary>
        public static void AddListener<T>(Action<T> listener)
        {
            if (listener == null)
            {
                return;
            }

            Type eventType = typeof(T);

            if (EventTable.TryGetValue(eventType, out Delegate existing))
            {
                EventTable[eventType] = Delegate.Combine(existing, listener);
            }
            else
            {
                EventTable[eventType] = listener;
            }
        }

        /// <summary>
        /// </summary>
        public static void RemoveListener<T>(Action<T> listener)
        {
            if (listener == null)
            {
                return;
            }

            Type eventType = typeof(T);
            if (!EventTable.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Delegate updated = Delegate.Remove(existing, listener);

            if (updated == null)
            {
                EventTable.Remove(eventType);
            }
            else
            {
                EventTable[eventType] = updated;
            }
        }

        /// <summary>
        /// </summary>
        public static void Broadcast<T>(T eventData)
        {
            Type eventType = typeof(T);
            if (!EventTable.TryGetValue(eventType, out Delegate existing))
            {
                return;
            }

            Action<T> callback = existing as Action<T>;
            callback?.Invoke(eventData);
        }

        /// <summary>
        /// </summary>
        public static void ClearAll()
        {
            EventTable.Clear();
        }
    }
}

