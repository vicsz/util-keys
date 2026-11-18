namespace Loupedeck.UtilKeysPlugin
{
    using System;

    // The last 60 samples, oldest first. Graphs draw from this.

    public class History
    {
        public const Int32 Size = 60;

        private readonly Double[] _samples = new Double[Size];
        private Int32 _next;
        private Int32 _count;

        public void Add(Double value)
        {
            lock (this._samples)
            {
                this._samples[this._next] = value;
                this._next = (this._next + 1) % Size;
                this._count = Math.Min(this._count + 1, Size);
            }
        }

        public Double[] ToArray()
        {
            lock (this._samples)
            {
                var values = new Double[this._count];

                for (var i = 0; i < this._count; i++)
                {
                    values[i] = this._samples[(this._next - this._count + i + Size) % Size];
                }

                return values;
            }
        }
    }
}
