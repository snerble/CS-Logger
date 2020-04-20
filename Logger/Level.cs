using System;
using System.Collections.Generic;
using System.Linq;

namespace Logging
{
	/// <summary>
	/// A class for creating logging levels.
	/// <para>This contains several default logging levels.</para>
	/// </summary>
	/// <seealso cref="DefaultLevels"/>
	public class Level
	{
		/// <summary>
		/// A collection of named <see cref="Level"/> instances.
		/// </summary>
		public static List<Level> Levels { get; } = new List<Level>();

		#region Default Levels
		/// <summary>
		/// Disables all log messages.
		/// </summary>
		public static readonly Level OFF = new Level(int.MinValue, "OFF");
		/// <summary>
		/// Mostly used for debugging code. 
		/// </summary>
		public static readonly Level DEBUG = new Level(-1000, "DEBUG", ConsoleColor.Magenta);
		/// <summary>
		/// Used for errors after which the program cannot continue running.
		/// </summary>
		public static readonly Level FATAL = new Level(-500, "FATAL", ConsoleColor.DarkRed);
		/// <summary>
		/// Used for errors that nonetheless do not prevent the program from continuing.
		/// </summary>
		public static readonly Level ERROR = new Level(-250, "ERROR", ConsoleColor.Red);
		/// <summary>
		/// Used to warn for things that may be out of the ordinary, but are otherwise not a problem.
		/// </summary>
		public static readonly Level WARN = new Level(-100, "WARN", ConsoleColor.DarkYellow);
		/// <summary>
		/// Used for general program information/feedback.
		/// </summary>
		public static readonly Level INFO = new Level(0, "INFO", ConsoleColor.Green);
		/// <summary>
		/// Used for logs that document program configuration events.
		/// </summary>
		public static readonly Level CONFIG = new Level(250, "CONFIG", ConsoleColor.Cyan);
		/// <summary>
		/// Used for relatively fine logging. Not as fine as TRACE.
		/// </summary>
		public static readonly Level FINE = new Level(500, "FINE", ConsoleColor.Gray);
		/// <summary>
		/// Used for very fine information. E.G object construction, function calls, etc.
		/// </summary>
		public static readonly Level TRACE = new Level(1000, "TRACE", ConsoleColor.DarkGray);
		/// <summary>
		/// Enables all log messages.
		/// </summary>
		public static readonly Level ALL = new Level(int.MaxValue, "ALL");
		#endregion

		/// <summary>
		/// Gets the name of this <see cref="Level"/>.
		/// </summary>
		public string Name { get; }
		/// <summary>
		/// Gets the numeric value of this <see cref="Level"/> for log record filtering.
		/// </summary>
		public int Value { get; }
		/// <summary>
		/// Gets the color of this <see cref="Level"/> when displayed on the <see cref="Console"/>.
		/// </summary>
		public ConsoleColor Color { get; }

		/// <summary>
		/// Creates a new instance of <see cref="Level"/>.
		/// </summary>
		/// <param name="value">The logging level value. This must be a unique value.</param>
		/// <param name="name">The name of the logging level. This is case sensitive and must be unique.</param>
		/// <returns>The newly created <see cref="Level"/> instance.</returns>
		/// <exception cref="ArgumentException">When <paramref name="name"/> or <paramref name="value"/> are not unique.</exception>
		public Level(int value, string name, ConsoleColor color = ConsoleColor.Gray)
		{
			Value = value;
			Name = name;
			Color = color;
			if (Levels.Any(x => x.Name == name)) throw new ArgumentException($"A level with the name '{name}' already exists.");
			Levels.Add(this);
		}

		/// <summary>
		/// Returns the name of this <see cref="Level"/>.
		/// </summary>
		public override string ToString() => Name;

		/// <summary>
		/// Returns a <see cref="Level"/> object with the same name, or null if none were found.
		/// </summary>
		/// <param name="name">The name of the <see cref="Level"/>.</param>
		/// <param name="caseSensitive">Sets whether the search is case sensitive or not.</param>
		public static Level GetLevel(string name, bool caseSensitive = false)
			=> Levels.FirstOrDefault(x => caseSensitive ? x.Name == name : x.Name.ToLower() == name.ToLower());
	}
}