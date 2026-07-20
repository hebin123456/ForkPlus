using System;
using System.Collections.Generic;

namespace ForkPlus.Jobs.Impl
{
	public class CircularArray<T>
	{
		private readonly T[] _items;

		private bool _full;

		private int _current;

		public int Count
		{
			get
			{
				if (_full)
				{
					return _items.Length;
				}
				return _current;
			}
		}

		public T this[int index]
		{
			get
			{
				if (_full)
				{
					if (index > _items.Length - 1)
					{
						throw new IndexOutOfRangeException();
					}
					return _items[(_current + index) % _items.Length];
				}
				if (index > _current - 1)
				{
					throw new IndexOutOfRangeException();
				}
				return _items[index];
			}
		}

		public CircularArray(int capacity)
		{
			_items = new T[capacity];
		}

		public void Add(T item)
		{
			_items[_current] = item;
			if (_current == _items.Length - 1)
			{
				_current = 0;
				_full = true;
			}
			else
			{
				_current++;
			}
		}

		public T[] ToArray()
		{
			T[] array = new T[Count];
			int count = Count;
			for (int i = 0; i < count; i++)
			{
				array[i] = this[i];
			}
			return array;
		}

		public T[] Filter(Func<T, bool> isIncluded)
		{
			int count = Count;
			List<T> list = new List<T>(count);
			for (int num = count - 1; num >= 0; num--)
			{
				T val = this[num];
				if (isIncluded(val))
				{
					list.Add(val);
				}
			}
			return list.ToArray();
		}
	}
}
