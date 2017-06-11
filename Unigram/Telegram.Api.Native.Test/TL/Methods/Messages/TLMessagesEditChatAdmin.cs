// <auto-generated/>
using System;
using Telegram.Api.Native.TL;

namespace Telegram.Api.TL.Methods.Messages
{
	/// <summary>
	/// RCP method messages.editChatAdmin.
	/// Returns <see cref="Telegram.Api.TL.TLBool"/>
	/// </summary>
	public partial class TLMessagesEditChatAdmin : TLObject
	{
		public Int32 ChatId { get; set; }
		public TLInputUserBase UserId { get; set; }
		public Boolean IsAdmin { get; set; }

		public TLMessagesEditChatAdmin() { }
		public TLMessagesEditChatAdmin(TLBinaryReader from)
		{
			Read(from);
		}

		public override TLType TypeId { get { return TLType.MessagesEditChatAdmin; } }

		public override void Read(TLBinaryReader from)
		{
			ChatId = from.ReadInt32();
			UserId = TLFactory.Read<TLInputUserBase>(from);
			IsAdmin = from.ReadBoolean();
		}

		public override void Write(TLBinaryWriter to)
		{
			to.WriteInt32(ChatId);
			to.WriteObject(UserId);
			to.WriteBoolean(IsAdmin);
		}
	}
}