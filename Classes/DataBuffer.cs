using System;
using System.Collections.Generic;
using System.Text;

namespace AgentSystem.Classes
{
    class DataBuffer
    {
        public Queue<object> Items { get; set; }
        public DataBuffer() { Items = new Queue<object>(); }
        public DataBuffer(int count) { Items = new Queue<object>(count); }

        static object buf_locker = new object();

        public void PutItem(object item)
        {
            lock (buf_locker)
            {
                Items.Enqueue(item);
            }
            return;
        }

        public object GetItem()
        {
            object item = null;
            lock (buf_locker)
            {
                if (Items.Count > 0)
                {
                    item = Items.Dequeue();
                }
            }
            return item;
        }

        public int GetCount()
        {
            int count;
            lock (buf_locker)
            {
                count = Items.Count;
            }
            return count;
        }

    }
}
