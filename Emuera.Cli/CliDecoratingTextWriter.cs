using System.IO;
using System.Text;

internal sealed class CliDecoratingTextWriter : TextWriter
{
	private readonly TextWriter inner;
	private readonly Func<string, string> decorateLine;
	private readonly StringBuilder pendingLine = new();

	public CliDecoratingTextWriter(TextWriter inner, Func<string, string> decorateLine)
	{
		this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		this.decorateLine = decorateLine ?? throw new ArgumentNullException(nameof(decorateLine));
	}

	public override Encoding Encoding => inner.Encoding;

	public override void Write(char value)
	{
		if (value == '\n')
		{
			FlushPendingLine();
			inner.Write(value);
			return;
		}

		pendingLine.Append(value);
	}

	public override void Write(string? value)
	{
		if (string.IsNullOrEmpty(value))
			return;

		var start = 0;
		for (var i = 0; i < value.Length; i++)
		{
			if (value[i] != '\n')
				continue;

			if (i > start)
				pendingLine.Append(value, start, i - start);
			FlushPendingLine();
			inner.Write('\n');
			start = i + 1;
		}

		if (start < value.Length)
			pendingLine.Append(value, start, value.Length - start);
	}

	public override void WriteLine(string? value)
	{
		Write(value);
		Write('\n');
	}

	public override void Flush()
	{
		if (pendingLine.Length > 0)
			FlushPendingLine();
		inner.Flush();
	}

	protected override void Dispose(bool disposing)
	{
		if (disposing)
			Flush();
		base.Dispose(disposing);
	}

	private void FlushPendingLine()
	{
		if (pendingLine.Length == 0)
			return;

		var line = pendingLine.ToString();
		pendingLine.Clear();
		inner.Write(decorateLine(line));
	}
}
