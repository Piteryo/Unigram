// <auto-generated/>
using System;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL
{
	public partial class TLChannelRoleModerator : TLChannelParticipantRoleBase 
	{
		public TLChannelRoleModerator() { }
		public TLChannelRoleModerator(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.ChannelRoleModerator; } }

		public override void Read(TLBinaryReader from)
		{
		}

		public override void Write(TLBinaryWriter to)
		{
			to.WriteUInt32(0x9618D975);
		}
	}
}