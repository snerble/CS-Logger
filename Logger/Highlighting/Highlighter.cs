using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Logging.Highlighting
{
	/// <summary>
	/// Represents a regular expression that colors the matched text in the <see cref="Console"/>.
	/// </summary>
	public readonly struct Highlighter
	{
		/// <summary>
		/// Gets the <see cref="Regex"/> that matches regions to which this <see cref="Highlighter"/> applies.
		/// </summary>
		public Regex Regex { get; }
		/// <summary>
		/// Gets the <see cref="ConsoleColor"/>s that specify the colors that each captured group will be.
		/// </summary>
		/// <remarks>
		/// If less colors are specified than the amount of captured groups, the last color in the collection
		/// will be used.
		/// </remarks>
		public IReadOnlyCollection<ConsoleColor> Colors { get; }

		/// <summary>
		/// Initializes a new <see cref="Highlighter"/> instance with the specified <see cref="System.Text.RegularExpressions.Regex"/>
		/// and group colors.
		/// </summary>
		/// <param name="regex">The <see cref="System.Text.RegularExpressions.Regex"/> to use for matching text.</param>
		/// <param name="colors">An array of colors used for each captured group.</param>
		public Highlighter(Regex regex, params ConsoleColor[] colors)
		{
			Regex = regex;
			Colors = Array.AsReadOnly(colors);
		}
		/// <summary>
		/// Initalizes a new instance of <see cref="Highlighter"/> with the specified keyword and color.
		/// </summary>
		/// <remarks>
		/// This builds a <see cref="System.Text.RegularExpressions.Regex"/> specifically for the specified keyword.
		/// </remarks>
		/// <param name="keyword">The text that should be highlighted by this highlighter.</param>
		/// <param name="color">The color to highlight matched text with.</param>
		public Highlighter(string keyword, ConsoleColor color) : this(new string[] { keyword }, new ConsoleColor[] { color }) { }
		/// <summary>
		/// Initalizes a new instance of <see cref="Highlighter"/> with the specified keywords and colors.
		/// </summary>
		/// <remarks>
		/// This builds a <see cref="System.Text.RegularExpressions.Regex"/> specifically for the specified keywords.
		/// </remarks>
		/// <param name="keyword">A collection of text that should be highlighted by this highlighter.</param>
		/// <param name="color">The colors to highlight the text, mapped to each keyword.</param>
		public Highlighter(IEnumerable<string> keywords, IEnumerable<ConsoleColor> colors)
		{
			Regex = new Regex("[^A-z0-9](?:" + string.Join("|", keywords.Select(x => $"({x})")) + ")(?![A-z0-9])");
			Colors = Array.AsReadOnly(colors.ToArray());
		}

		/// <summary>
		/// Returns a new <see cref="HighlightedRegionCollection"/> containing all regions
		/// matched by this <see cref="Highlighter"/>.
		/// </summary>
		/// <param name="input"></param>
		internal HighlightedRegionCollection GetHighlights(ref string input)
			=> new HighlightedRegionCollection(this, ref input);
	}
}
