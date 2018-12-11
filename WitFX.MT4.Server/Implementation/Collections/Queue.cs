namespace WitFX.MT4.Server.Implementation.Collections
{
    public sealed class Queue<T> : System.Collections.Generic.Queue<T>
    {
        public bool empty() => Count == 0;
        public T front() => Peek();
        public T pop() => Dequeue();
    }
}
