using System;
using System.Drawing;

namespace MinorShift.Emuera.UI.Game.Image;

internal sealed class ConstImage : AbstractImage
{
	public ConstImage(string name)
	{ Name = name; RealIsCreated = false; }

	public readonly string Name;
	public Bitmap RealBitmap;
	public string Filepath;
	public int Width;
	public int Height;
	public bool RealIsCreated;

	internal void CreateFrom(Bitmap bmp, string filepath, bool useGDI)
	{
		if (RealBitmap != null || !string.IsNullOrEmpty(Filepath))
			throw new Exception();
		//呼び出し元でファイルチェックはしてるから大丈夫だと思う……一応1000回上限
		int i = 0;
		while (i++ < 1000)
		{
			try
			{
				RealBitmap = bmp;
				Filepath = filepath;
				Width = RealBitmap.Width;
				Height = RealBitmap.Height;
				AppContents.tempLoadedConstImages.Add(this);
				RealIsCreated = true;
				return;
			}
			catch
			{
			}
		}
		return;
	}

	public void Load()
	{
		if (RealBitmap != null || !RealIsCreated)
			return;
		try
		{
			RealBitmap = ImgUtils.LoadImage(Filepath);
			if (RealBitmap == null)
			{
				return;
			}
			AppContents.tempLoadedConstImages.Add(this);
		}
		catch
		{
			return;
		}
		return;
	}
	//public void Load(bool useGDI)
	//{
	//	if (Loaded)
	//		return;
	//	try
	//	{
	//		Bitmap = new Bitmap(Filepath);
	//		if (useGDI)
	//		{
	//			hBitmap = Bitmap.GetHbitmap();
	//			g = Graphics.FromImage(Bitmap);
	//			GDIhDC = g.GetHdc();
	//			hDefaultImg = GDI.SelectObject(GDIhDC, hBitmap);
	//		}
	//		Loaded = true;
	//		Enabled = true;
	//	}
	//	catch
	//	{
	//		return;
	//	}
	//	return;
	//}

	public override void Dispose()
	{
		if (RealBitmap == null || !RealIsCreated)
			return;
		if (g != null)
		{
			g.Dispose();
			g = null;
		}
		if (RealBitmap != null)
		{
			RealBitmap.Dispose();
			RealBitmap = null;
		}
	}

	~ConstImage()
	{
		Dispose();
	}


	public override bool IsCreated
	{
		get
		{
			return RealIsCreated;
		}
	}

	public override Bitmap Bitmap
	{
		set
		{
			RealBitmap = value;
		}
		get
		{
			Load();
			return RealBitmap;
		}
	}
}
