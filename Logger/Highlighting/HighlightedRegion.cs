using System;

namespace Logging.Highlighting
{
	/// <summary>
	/// Represents a range of colored text in the <see cref="Console"/>.
	/// </summary>
	internal struct HighlightedRegion
	{
		/// <summary>
		/// Gets the <see cref="Range"/> of this <see cref="HighlightedRegion"/> on the input string.
		/// </summary>
		public Range Range { get; }
		/// <summary>
		/// Gets the color of this <see cref="HighlightedRegion"/>.
		/// </summary>
		public ConsoleColor Color { get; }

		/// <summary>
		/// Initializes a new instance of <see cref="HighlightedRegion"/> with the specified range and color.
		/// </summary>
		/// <param name="range">The range of this <see cref="HighlightedRegion"/> on the input string.</param>
		/// <param name="color">The color of the text in the range of this <see cref="HighlightedRegion"/>.</param>
		public HighlightedRegion(Range range, ConsoleColor color)
		{
			Range = range;
			Color = color;
		}
		/// <summary>
		/// Initializes a new <see cref="HighlightedRegion"/> with the specified starting index, length and color.
		/// </summary>
		/// <param name="index">The starting index of the matched text from the input string.</param>
		/// <param name="length">The length of the matched text from the input string.</param>
		/// <param name="color">The color of the text in the range of this <see cref="HighlightedRegion"/>.</param>
		public HighlightedRegion(int index, int length, ConsoleColor color) : this(new Range(index, index + length), color) { }

		/// <summary>
		/// Returns whether this <see cref="HighlightedRegion"/> fully encapsulates the other
		/// <see cref="HighlightedRegion"/>.
		/// </summary>
		/// <param name="other">The <see cref="HighlightedRegion"/> to test.</param>
		/// <returns></returns>
		public bool Contains(HighlightedRegion other)
			=> Range.Start.Value <= other.Range.Start.Value && Range.End.Value >= other.Range.End.Value;
		/// <summary>
		/// Returns whether the given index lands anywhere inside this <see cref="HighlightedRegion"/>.
		/// </summary>
		/// <param name="index">The index to test on this <see cref="HighlightedRegion"/>.</param>
		public bool Contains(int index) => Range.Start.Value <= index && Range.End.Value >= index;
	}
}
