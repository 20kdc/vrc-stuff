using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace KDCVRCBSP.ECL {
	/// Read-only entity keys.
	/// This is useful where modification of entity keys isn't useful.
	public class ROEntityKeys: IReadOnlyList<(string, string)> {
		/// Underlying key list.
		protected List<(string, string)> backingList = new();

		/// Maps a key to its value.
		/// Contains the *LAST* value in the list
		protected Dictionary<string, string> backingDict = new();

		public ROEntityKeys() {
		}

		public ROEntityKeys(IEnumerable<(string, string)> copyThis) {
			foreach (var pair in copyThis) {
				backingList.Add(pair);
				backingDict[pair.Item1] = pair.Item2;
			}
		}

		public string this[string key] {
			get {
				if (backingDict.TryGetValue(key, out string value)) {
					return value;
				}
				return "";
			}
		}

		public (string, string) this[int index] {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => backingList[index];
		}

		public int Count {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => backingList.Count;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		IEnumerator IEnumerable.GetEnumerator() => backingList.GetEnumerator();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public IEnumerator<(string, string)> GetEnumerator() => backingList.GetEnumerator();

		/// Returns the index of a given key.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public int IndexOf(string key, int startIndex = 0) {
			while (startIndex < backingList.Count) {
				if (backingList[startIndex].Item1 == key)
					return startIndex;
				startIndex++;
			}
			return -1;
		}

		// -- Parser-Getters --

		public Vector3d GetVector(string key, Vector3d defaultVal) {
			string[] s3 = this[key].Split(' ');
			if (s3.Length == 3)
				if (float.TryParse(s3[0], out var x))
					if (float.TryParse(s3[1], out var y))
						if (float.TryParse(s3[2], out var z))
							return new Vector3d(x, y, z);
			return defaultVal;
		}

		public bool GetBool(string key, bool defaultVal) {
			if (this[key] == "1")
				return true;
			if (this[key] == "0")
				return false;
			return defaultVal;
		}

		public int GetInt(string key, int defaultVal) {
			if (int.TryParse(this[key], out var val))
				return val;
			return defaultVal;
		}

		public E GetEnum<E>(string key, E defaultVal) where E : struct {
			if (Enum.TryParse<E>(this[key], out E res))
				return res;
			return defaultVal;
		}

		public float GetFloat(string key, float defaultVal) {
			if (float.TryParse(this[key], out var val))
				return val;
			return defaultVal;
		}

		public double GetDouble(string key, double defaultVal) {
			if (double.TryParse(this[key], out var val))
				return val;
			return defaultVal;
		}
	}

	/// Represents entity key/value pairs.
	public class EntityKeys: ROEntityKeys {
		public EntityKeys(): base() {
		}

		public EntityKeys(IEnumerable<(string, string)> copyThis): base(copyThis) {
		}

		/// Shadowing here is a pretty messy trick, but it has the desired effect.
		public new string this[string key] {
			get {
				if (backingDict.TryGetValue(key, out string value)) {
					return value;
				}
				return "";
			}
			set {
				Remove(key);
				backingDict[key] = value;
				backingList.Add((key, value));
			}
		}

		/// Adds a pair.
		/// Everything else is optimized around both this and the simple field lookup case.
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Add((string, string) pair) {
			backingList.Add(pair);
			backingDict[pair.Item1] = pair.Item2;
		}

		/// Remove all uses of the given key.
		public void Remove(string key) {
			backingDict.Remove(key);
			int idx = 0;
			while (true) {
				idx = IndexOf(key, idx);
				if (idx == -1)
					break;
				backingList.RemoveAt(idx);
			}
		}

		/// Remove the key at the given index.
		public void RemoveAt(int index) {
			string key = backingList[index].Item1;
			backingList.RemoveAt(index);
			backingDict.Remove(key);
			// Note we use the **last** value received here.
			// This is consistent with how Add works here
			foreach (var pair in backingList)
				if (pair.Item1 == key)
					backingDict[key] = pair.Item2;
		}
	}
}
