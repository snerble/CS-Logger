using Logging.Highlighting;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;

namespace Logging
{
	/// <summary>
	/// General purpose logging class, influenced largely by the logging class in Python.
	/// <para>
	/// These loggers support a hierarchy structure of <see cref="Logger"/> objects,
	/// where one parent logger passes its log messages to any child loggers.
	/// </para>
	/// </summary>
	public class Logger : IDisposable
	{
		/// <summary>
		/// Gets or sets name of this logger object
		/// </summary>
		public string Name { get; set; }
		/// <summary>
		/// The time when this logger was created
		/// </summary>
		public DateTime Created { get; } = DateTime.Now;

		/// <summary>
		/// A read-only collection of associated loggers
		/// </summary>
		public IReadOnlyCollection<Logger> Children => _children.AsReadOnly();
		private readonly List<Logger> _children = new List<Logger>();

		/// <summary>
		/// A read-only collection of loggers this object is associated with.
		/// </summary>
		public IReadOnlyCollection<Logger> Parents => _parents.AsReadOnly();
		private readonly List<Logger> _parents = new List<Logger>();

		/// <summary>
		/// The collection of <see cref="TextWriter"/> objects this logger writes to.
		/// </summary>
		/// <remarks>
		/// When removing streams, make sure to close and/or dispose them, as this does not happen automatically.
		/// </remarks>
		public List<TextWriter> OutputStreams { get; } = new List<TextWriter>();

		public static List<Highlighter> Highlighters { get; } = new List<Highlighter>()
		{
			// Strings
			new Highlighter(new Regex("(\"|')((?:\\\\\\1|(?:(?!\\1).))*)(\"|')", RegexOptions.Compiled),
				ConsoleColor.Red),
			// Timestamps
			new Highlighter(new Regex(@"((?:[01]?[0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9])?", RegexOptions.Compiled),
				ConsoleColor.DarkGreen),
			// Stacktrace
			new Highlighter(new Regex(@"^\s+(at\s(?:.(?!\sin\s))*.)(?:\s(in)\s((?:.(?!:line))*.)|)(?::line\s\d+|)", RegexOptions.Multiline | RegexOptions.Compiled),
				ConsoleColor.DarkRed,
				ConsoleColor.Red,
				ConsoleColor.DarkRed),
			// Numbers
			new Highlighter(new Regex(@"([+-]?(?=\.\d|\d)(?:\d+)?(?:\.?\d*))(?:[eE]([+-]?\d+))?", RegexOptions.Compiled),
				ConsoleColor.Blue),
			// Booleans
			new Highlighter(new string[] { bool.TrueString.ToLower(), bool.FalseString.ToLower() }, new ConsoleColor[] { ConsoleColor.Blue }),
			// Log level keywords
			new Highlighter(Level.Levels.Select(x => x.Name), Level.Levels.Select(x => x.Color)),
		};

		/// <summary>
		/// The current logging level. This can be changed at any time.
		/// </summary>
		public Level LogLevel { get; set; }

		/// <summary>
		/// Gets or sets the format used for log records.
		/// <para/>
		/// Default value is:
		/// <code>{asctime:HH:mm:ss} {classname,-10} {levelname,6}: {message}</code>
		/// </summary>
		public string Format { get; set; } = "{asctime:HH:mm:ss} {classname,-10} {levelname,6}: {message}";

		/// <summary>
		/// Sets whether or not the log record stacktraces will use file info.
		/// </summary>
		public bool UseFileInfo { get; set; } = true;
		/// <summary>
		/// Gets or sets whether this <see cref="Logger"/> uses <see cref="Highlighter"/> instances to
		/// color the output written to <see cref="Console.Out"/>.
		/// </summary>
		public bool UseConsoleHighlighting { get; set; } = true;

		/// <summary>
		/// Disables logging for this instance and without changing the logging level.
		/// <para>This also prevents writing to child loggers, but it does not silence it's children.</para>
		/// </summary>
		public bool Silent { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating the scheduling priority of the logging thread.
		/// <para/>
		/// Setting a non-<see langword="null"/> value will disable the dynamic thread priority
		/// system.
		/// </summary>
		public ThreadPriority? Priority
		{
			get => worker.Priority;
			set
			{
				if (!value.HasValue)
				{
					worker.Priority = ThreadPriority.Highest;
					overridePriority = false;
				}
				else
				{
					overridePriority = true;
					worker.Priority = value.Value;
				}
			}
		}

		private Thread worker;
		private bool running = true;
		private BlockingCollection<Record> records = new BlockingCollection<Record>();
		private bool overridePriority = false;

		/// <summary>
		/// Creates a new instance of <see cref="Logger"/>.
		/// </summary>
		/// <remarks>
		/// This constructor supports custom log levels.
		/// </remarks>
		/// <param name="level">The maximum logging level, represented as <see cref="int"/>.</param>
		/// <param name="outStreams">A collection of unique <see cref="TextWriter"/> objects.</param>
		public Logger(Level level, params TextWriter[] outStreams)
			: this(level, null, outStreams)
		{ }
		/// <summary>
		/// Creates a new instance of <see cref="Logger"/> with a custom name and format.
		/// </summary>
		/// <remarks>
		/// This constructor supports custom log levels.
		/// </remarks>
		/// <param name="level">The maximum logging level, represented as <see cref="int"/>.</param>
		/// <param name="format">The format used for log records. If null, the default format is used.</param>
		/// <param name="outStreams">A collection of unique <see cref="TextWriter"/> objects.</param>
		public Logger(Level level, string format, params TextWriter[] outStreams)
		{
			LogLevel = level;
			foreach (TextWriter stream in outStreams)
				OutputStreams.Add(stream);
			Name = $"{GetType().Name}@{GetHashCode():x}";
			Format = format ?? Format;

			worker = new Thread(Run)
			{
				Name = "Logger Worker",
			};
			worker.Start();
		}

		private void Run()
		{
			try
			{
				while (running)
				{
					// Await a new record
					Record record = records.Take();
					
					if (!overridePriority)
					{
						// Dynamically adjust the worker thread priority by checking the amount of remaining records
						worker.Priority = records.Count switch
						{
							int n when (100 > n) => ThreadPriority.BelowNormal,
							int n when (250 > n && n >= 100) => ThreadPriority.Normal,
							int n when (1000 > n && n >= 500) => ThreadPriority.AboveNormal,
							int n when (n >= 1000) => ThreadPriority.Highest,
							_  => ThreadPriority.Normal // this case label should never match
						};
					}

					Write(record);
				}
			}
			catch (InvalidOperationException) { }
			catch (ThreadInterruptedException) { }
		}

		#region Default Logging Methods
		/// <summary>
		/// Writes a message with the DEBUG log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Debug(object message, bool stackTrace = false) => Write(Level.DEBUG, message, stackTrace);

		/// <summary>
		/// Writes a message with the FATAL log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Fatal(object message, bool stackTrace = false) => Write(Level.FATAL, message, stackTrace);
		/// <summary>
		/// Writes a message with the FATAL log level. This includes an exception traceback.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="cause">The exception that caused this message.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Fatal(object message, Exception cause, bool stackTrace = true) => records.Add(new Record(Level.FATAL, message, new StackTrace(cause, UseFileInfo), stackTrace));

		/// <summary>
		/// Writes a message with the ERROR log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Error(object message, bool stackTrace = false) => Write(Level.ERROR, message, stackTrace);
		/// <summary>
		/// Writes a message with the ERROR log level. This includes an exception traceback.
		/// <para>The traceback of the given exception will be used, where the most recent calls will end at the cause.</para>
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="cause">The exception that caused this message. This exception's traceback will be used.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Error(object message, Exception cause, bool stackTrace = true) => records.Add(new Record(Level.ERROR, message, new StackTrace(cause, UseFileInfo), stackTrace));

		/// <summary>
		/// Writes a message with the WARN log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Warning(object message, bool stackTrace = false) => Write(Level.WARN, message, stackTrace);

		/// <summary>
		/// Writes a message with the INFO log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Info(object message, bool stackTrace = false) => Write(Level.INFO, message, stackTrace);

		/// <summary>
		/// Writes a message with the CONFIG log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Config(object message, bool stackTrace = false) => Write(Level.CONFIG, message, stackTrace);

		/// <summary>
		/// Writes a message with the FINE log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Fine(object message, bool stackTrace = false) => Write(Level.FINE, message, stackTrace);

		/// <summary>
		/// Writes a message with the TRACE log level.
		/// </summary>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Trace(object message, bool stackTrace = false) => Write(Level.TRACE, message, stackTrace);
		#endregion

		/// <summary>
		/// Writes the log to the output streams if the level is lower or equal to the set logging level.
		/// </summary>
		/// <param name="level">A <see cref="Level"/> message level.</param>
		/// <param name="message">The value to write.</param>
		/// <param name="stackTrace">Set whether to include a full stacktrace in the log record.</param>
		public void Write(Level level, object message, bool stackTrace = false) => records.Add(new Record(level, message, new StackTrace(UseFileInfo), stackTrace));
		/// <summary>
		/// Writes the log to the output streams if the level is lower or equal to the set logging level.
		/// <para>This function is thread-safe due to it's stream locking.</para>
		/// </summary>
		/// <param name="level">A <see cref="Level"/> message level.</param>
		/// <param name="message">The value to write.</param>
		/// <param name="stack">The stacktrace to reference in the log record.</param>
		/// <param name="includeStackTrace">Set whether to include a full stacktrace in the log record.</param>
		private void Write(Record record)
		{
			// Deconstruct the record
			(Level level, object message, StackTrace stack, bool includeStackTrace, _) = record;

			if (Silent) return;
			if (disposedValue) throw new ObjectDisposedException(ToString());

			foreach (Logger logger in Children) logger.Write(record);
			if (LogLevel.Value < level.Value) return;

			// Get the formatted log record
			var logMessage = GetRecord(record);

			// Write the record to the the Trace class if no outputstreams are available
			if (!OutputStreams.Any())
				System.Diagnostics.Trace.WriteLine(logMessage);
			else
			{
				// Write the log record to every stream
				foreach (TextWriter stream in OutputStreams)
				{
					lock (stream)
					{
						if (UseConsoleHighlighting && stream == Console.Out)
							WriteConsoleRecord(logMessage);
						else
						{
							stream.WriteLine(logMessage);
							stream.Flush();
						}
					}
				}
			}
		}

		/// <summary>
		/// Writes the given record to the console and highlights certain parts of the line.
		/// </summary>
		/// <param name="record">The record to write to the console.</param>
		protected void WriteConsoleRecord(string record)
		{
			if (!Highlighters.Any()) return;

			// Create base highlighter collection instance
			HighlightedRegionCollection highlights = Highlighters[0].GetHighlights(ref record);
			// Combine all the other collections with the first
			for (int i = 1; i < Highlighters.Count; i++)
				highlights.AddRange(Highlighters[i].GetHighlights(ref record));

			// Write the record using the highlight colors
			highlights.Print();
		}

		/// <summary>
		/// Adds a new child logger to this logger.
		/// <para>All logging messages to this logger will be passed on to its child loggers.</para>
		/// </summary>
		/// <param name="logger">The <see cref="Logger"/> object to add to this logger.</param>
		public void Attach(Logger logger)
		{
			_children.Add(logger);
			logger._parents.Add(this);
		}
		/// <summary>
		/// Removes a logger from this object's children.
		/// </summary>
		/// <param name="logger">The logger to remove.</param>
		public void Detach(Logger logger)
		{
			_children.Remove(logger);
			logger._parents.Remove(this);
		}

		/// <summary>
		/// Closes the associated <see cref="TextWriter"/> objects and <see cref="Logger"/> children.
		/// </summary>
		public void Close()
		{
			foreach (Logger logger in Children)
			{
				logger.Close();
				_children.Remove(logger);
				foreach (Logger parent in Parents) parent.Detach(this);
			}
			foreach (TextWriter stream in OutputStreams) stream.Close();
		}

		/// <summary>
		/// Returns a formatted log record based on this logger instance.
		/// </summary>
		/// <remarks>
		/// TL;DR - Big dumb block of code that fills in the blanks. Replaces and formats stuff. Makes the log look nice.
		/// </remarks>
		/// <param name="msg">The string to format.</param>
		protected string GetRecord(Record record)
		{
			(
				Level level,
				object message,
				StackTrace stack,
				bool includeStackTrace,
				DateTime creation
			) = record;

			// check if stackinfo has been specified. if not, stack info will always be appended.
			bool appendStackTrace = !Regex.IsMatch(Format, ".*{stackinfo.*", RegexOptions.IgnoreCase);

			// replace all available attributes for records with a number for string formatting.
			string format = Format;
			foreach (var value in Enum.GetValues(typeof(RecordAttributes)).Cast<int>().Reverse())
			{
				var name = Enum.GetName(typeof(RecordAttributes), value);
				format = Regex.Replace(format, '{' + name, '{' + value.ToString(), RegexOptions.IgnoreCase);
			}
			// prepare local fields extracted from the stacktrace
			Type callerType = null;
			MethodBase callerFunc = null;
			int? lineno = null;
			string fileName = null;
			string stackTrace = stack.ToString();

			// loops through all stacktrace frames and fills in the previous values
			int i = 0;
			foreach (StackFrame frame in stack.GetFrames())
			{
				i++;
				callerFunc = frame.GetMethod();
				fileName = frame.GetFileName();
				lineno = frame.GetFileLineNumber();
				if (i != stack.FrameCount && callerType == GetType() && callerFunc.DeclaringType == GetType() && stack.GetFrame(i).GetMethod().DeclaringType == GetType())
				{
					callerFunc = stack.GetFrame(i).GetMethod();
					break; // Internal logging calls (log calls from the Logger class) only go through the default logging level methods,
						   // meaning once 3 calls originating from Logger have been found, we can be sure the call actually came from the Logger class.
				}
				callerType = callerFunc.DeclaringType;
				if (callerType != GetType())
					break;
			}
			if (i <= stack.FrameCount)
			{
				// trim chained logging calls from the stacktrace. These calls are after the first logging call and are irrelevant.
				var stacklines = stackTrace.Split("\r\n");
				stackTrace = stacklines[i-1];
                for (; i < stacklines.Length; i++)
                    stackTrace += "\r\n" + stacklines[i];
			}

			// format all attributes 
			return string.Format(format,
				creation,
				((DateTimeOffset)creation).ToUnixTimeMilliseconds()/1000d,
				callerType?.Name,
				Path.GetFileName(fileName),
				callerFunc.Name,
				level,
				level.Value,
				lineno,
				message?.ToString(),
				callerFunc.Module,
				Name,
				fileName,
				Process.GetCurrentProcess().Id,
				Process.GetCurrentProcess().ProcessName,
				creation - Process.GetCurrentProcess().StartTime,
				includeStackTrace ? "\n" + stackTrace.ToString().Remove(stackTrace.ToString().Length - 2, 2).Remove(0, 0) : null,
				Thread.CurrentThread.ManagedThreadId,
				Thread.CurrentThread.Name
			) + (appendStackTrace && includeStackTrace ? "\n" + stackTrace.ToString().Remove(stackTrace.ToString().Length - 2, 2) : null);
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					running = false;
					records.CompleteAdding();
					worker?.Interrupt();
					worker?.Join();
					
					// New list copy because otherwise we are changing the list we are iterating through
					foreach (Logger logger in new List<Logger>(Children))
						logger.Dispose();
					foreach (TextWriter stream in OutputStreams)
						stream.Dispose();
					// Same reason for new list as previous
					foreach (Logger parent in new List<Logger>(Parents)) parent.Detach(this);
				}

				disposedValue = true;
			}
		}

		/// <summary>
		/// Disposes the current 
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion

		/// <summary>
		/// An enum of attributes used in log records
		/// </summary>
		public enum RecordAttributes
		{
			asctime,
			created,
			className,
			fileName,
			funcName,
			levelName,
			levelno,
			lineno,
			message,
			module,
			name,
			pathname,
			processId,
			processName,
			relativeCreated,
			stackInfo,
			threadId,
			threadName
		}

		protected struct Record
		{
			public Level level;
			public object message;
			public StackTrace stackTrace;
			public bool includeStackTrace;
			public DateTime creation;

			public Record(Level level, object message, StackTrace stackTrace, bool includeStackTrace)
			{
				this.level = level ?? throw new ArgumentNullException(nameof(level));
				this.message = message ?? throw new ArgumentNullException(nameof(message));
				this.stackTrace = stackTrace ?? throw new ArgumentNullException(nameof(stackTrace));
				this.includeStackTrace = includeStackTrace;
				creation = DateTime.Now;
			}

			public void Deconstruct(out Level level, out object message, out StackTrace stackTrace, out bool includeStackTrace, out DateTime creation)
			{
				level = this.level;
				message = this.message;
				stackTrace = this.stackTrace;
				includeStackTrace = this.includeStackTrace;
				creation = this.creation;
			}
		}
	}
}