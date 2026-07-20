using System.Collections.Generic;

namespace ForkPlus
{
	public class LruCache<TKey, TValue>
	{
		private class Node<TNodeKey, TNodeValue>
		{
			public TNodeKey Key { get; }

			public TNodeValue Value { get; set; }

			public Node<TNodeKey, TNodeValue> Prev { get; set; }

			public Node<TNodeKey, TNodeValue> Next { get; set; }

			public Node(TNodeKey key, TNodeValue value)
			{
				Key = key;
				Value = value;
			}
		}

		private readonly Dictionary<TKey, Node<TKey, TValue>> _cache;

		private readonly int _capacity;

		private Node<TKey, TValue> _root;

		private Node<TKey, TValue> _tail;

		public LruCache(int capacity)
		{
			_capacity = capacity;
			_cache = new Dictionary<TKey, Node<TKey, TValue>>(_capacity);
		}

		public bool TryGet(TKey key, out TValue value)
		{
			if (_cache.TryGetValue(key, out var value2))
			{
				MoveToRoot(value2);
				value = value2.Value;
				return true;
			}
			value = default(TValue);
			return false;
		}

		public void Put(TKey key, TValue value)
		{
			if (_cache.TryGetValue(key, out var value2))
			{
				MoveToRoot(value2);
				value2.Value = value;
				return;
			}
			Node<TKey, TValue> node = new Node<TKey, TValue>(key, value);
			if (_root == null)
			{
				_root = node;
				_tail = node;
			}
			else
			{
				node.Next = _root;
				_root.Prev = node;
				_root = node;
			}
			_cache[key] = node;
			if (_cache.Keys.Count > _capacity && _tail != null)
			{
				RemoveLast();
			}
		}

		private void MoveToRoot(Node<TKey, TValue> node)
		{
			if (node != _root)
			{
				Node<TKey, TValue> prev = node.Prev;
				if (prev != null)
				{
					prev.Next = node.Next;
				}
				Node<TKey, TValue> next = node.Next;
				if (next != null)
				{
					next.Prev = node.Prev;
				}
				else if (node == _tail)
				{
					_tail = node.Prev;
				}
				node.Next = _root;
				_root.Prev = node;
				_root = node;
				_root.Prev = null;
			}
		}

		private void RemoveLast()
		{
			_cache.Remove(_tail.Key);
			_tail = _tail.Prev;
			if (_tail != null)
			{
				_tail.Next = null;
			}
		}
	}
}
