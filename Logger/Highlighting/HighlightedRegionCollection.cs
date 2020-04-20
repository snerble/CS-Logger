using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;

namespace Logging.Highlighting
{
	/// <summary>
	/// A collection of <see cref="HighlightedRegion"/>s that automatically sorts and filters it's
	/// elements.
	/// </summary>
	internal class HighlightedRegionCollection : ICollection<HighlightedRegion>
	{
		public string InputText { get; }
		private readonly List<HighlightedRegion> Elements = new List<HighlightedRegion>();

		/// <summary>
		/// Gets or sets a single element from this collection at the specified index.
		/// </summary>
		/// <param name="index">The index of the item to get or set.</param>
		HighlightedRegion this[int index] => Elements[index];

		/// <summary>
		/// Gets the amount of elements in this collection.
		/// </summary>
		public int Count => Elements.Count;
		public bool IsReadOnly => false;

		/// <summary>
		/// Initializes a new instance of <see cref="HighlightedRegionCollection"/> with the specified
		/// highlighter and input string.
		/// </summary>
		/// <param name="input">The string from which this collection was produced. The matched text is automatically removed.</param>
		public HighlightedRegionCollection(Highlighter highlighter, ref string input)
		{
			InputText = input;

			var matches = highlighter.Regex.Matches(input);
			foreach (Match match in matches)
			{
				// Replace the captured text with null characters
				int colorIndex = 0;
				bool first = true;
				foreach (Group group in match.Groups)
				{
					// Skip empty groups
					if (group.Length == 0)
					{
						// Increment color index if more colors are available
						if (colorIndex + 1 < highlighter.Colors.Count) colorIndex++;
						continue;
					}
					if (first)
					{
						first = false;
						continue;
					}
					// Create new region
					Add(new HighlightedRegion(group.Index, group.Length, highlighter.Colors.ElementAt(colorIndex)));
					// Increment color index if more colors are available
					if (colorIndex + 1 < highlighter.Colors.Count) colorIndex++;
					// Remove the captured text from the input string
					char[] _input = input.ToCharArray();
					new string('\0', group.Length).CopyTo(0, _input, group.Index, group.Length);
					input = string.Concat(_input);
				}
			}
		}

		/// <summary>
		/// Writes the input string to the <see cref="Console"/> while applying the <see cref="HighlightedRegion"/>s.
		/// </summary>
		public void Print()
		{
			Print(new HighlightedRegion(0, InputText.Length, Console.ForegroundColor));
			Console.WriteLine();
		}
		/// <summary>
		/// Writes all regions recursively.
		/// </summary>
		private void Print(HighlightedRegion current)
		{
			// Cache the current color for later and apply the current highlight color
			var prevColor = Console.ForegroundColor;
			Console.ForegroundColor = current.Color;

			// Store offset because nested regions will shift this
			int offset = current.Range.Start.Value;

			// Get the index of a nested region (if any) (searchOffset helps to skip deeper nested regions)
			var searchOffset = offset;
			var nestedRegions = this.Where(x =>
			{
				if (!x.Equals(current) && x.Range.Start.Value >= searchOffset && current.Contains(x))
				{
					searchOffset = x.Range.End.Value;
					return true;
				}
				return false;
			}).ToList();
			// Loop through all nested regions
			foreach (var nested in nestedRegions)
			{
				// Write up until the nested region if the start indices aren't equal
				if (!nested.Range.Start.Equals(offset))
					Console.Write(InputText[offset..nested.Range.Start.Value]);
				// Recurse with the nested region
				Print(nested);
				// Shift the offset to after the nested region
				offset = nested.Range.End.Value;
			}
			// Write the remaining text if the offset does not exceed the end index
			if (offset <= current.Range.End.Value - 1)
				Console.Write(InputText[offset..current.Range.End]);

			// Reset to the previous color
			Console.ForegroundColor = prevColor;
		}

		/// <summary>
		/// Appends the elements of the specified collection to this collection.
		/// </summary>
		/// <param name="collection">The collection whose elements to add to this collection.</param>
		public void AddRange(IEnumerable<HighlightedRegion> collection)
		{
			foreach (var item in collection)
				Add(item);
		}
		/// <summary>
		/// Adds the specified item to this collection.
		/// </summary>
		/// <param name="item">The item to add.</param>
		public void Add(HighlightedRegion item)
		{
			//// Remove existing elements that are identical in range to the new item
			var toRemove = this.Where(x => item.Range.Equals(x.Range)).ToList();
			foreach (var region in toRemove) Remove(region);

			// Get the index to insert the new item into
			int index = 0;
			for (; index < Count; index++)
			{
				if (this[index].Range.Start.Value > item.Range.Start.Value) break;
				else if(this[index].Range.Start.Equals(item.Range.Start))
				{
					index++;
					break;
				}
			}
				
			// Insert the new item
			Elements.Insert(index, item);
		}

		/// <summary>
		/// Removes all elements from this collection.
		/// </summary>
		public void Clear() => Elements.Clear();
		/// <summary>
		/// Returns whether this collection contains the specified element.
		/// </summary>
		/// <param name="item">The item to find in this collection.</param>
		public bool Contains(HighlightedRegion item) => Elements.Contains(item);
		/// <summary>
		/// Copies the elements of this collection to the specified array.
		/// </summary>
		/// <param name="array">The array to copy this collection's elements to.</param>
		/// <param name="arrayIndex">The index from which the copying begins.</param>
		public void CopyTo(HighlightedRegion[] array, int arrayIndex) => Elements.CopyTo(array, arrayIndex);
		/// <summary>
		/// Removes an element from this collection.
		/// </summary>
		/// <param name="item">The element to remove.</param>
		public bool Remove(HighlightedRegion item) => Elements.Remove(item);

		/// <summary>
		/// Returns an enumerator over this collection's elements.
		/// </summary>
		public IEnumerator<HighlightedRegion> GetEnumerator()
			=> Elements.GetEnumerator();
		/// <summary>
		/// Returns an enumerator over this collection's elements.
		/// </summary>
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
