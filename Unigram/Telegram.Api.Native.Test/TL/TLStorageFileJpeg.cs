// <auto-generated/>
using System;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL
{
	public partial class TLStorageFileJpeg : TLStorageFileTypeBase 
	{
		public TLStorageFileJpeg() { }
		public TLStorageFileJpeg(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.StorageFileJpeg; } }

		public override void Read(TLBinaryReader from)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.WriteUInt32(0x7EFE0E);
		}
	}
}