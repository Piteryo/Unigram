using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache.EventArgs;
using Telegram.Api.TL;
using Telegram.Api.TL.Messages;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Views;
using Unigram.Converters;
using Unigram.Helpers;
using Unigram.Native;
using Unigram.Services;
using Unigram.Views;
using Unigram.Views.Payments;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace Unigram.ViewModels
{
    public partial class DialogViewModel
    {
        #region Reply

        public RelayCommand<TLMessageBase> MessageReplyCommand { get; }
        private void MessageReplyExecute(TLMessageBase message)
        {
            Search = null;

            if (message == null)
            {
                return;
            }

            var serviceMessage = message as TLMessageService;
            if (serviceMessage != null)
            {
                var action = serviceMessage.Action;
                // TODO: 
                //if (action is TLMessageActionEmpty || action is TLMessageActionUnreadMessages)
                //{
                //    return;
                //}
            }

            if (message.Id <= 0) return;

            var message31 = message as TLMessage;
            if (message31 != null && !message31.IsOut && message31.HasFromId)
            {
                var fromId = message31.FromId.Value;
                var user = CacheService.GetUser(fromId) as TLUser;
                if (user != null && user.IsBot)
                {
                    // TODO: SetReplyMarkup(message31);
                }
            }

            Reply = message;
            TextField.Focus(Windows.UI.Xaml.FocusState.Keyboard);
        }

        #endregion

        #region Delete

        public RelayCommand<TLMessageBase> MessageDeleteCommand { get; }
        private async void MessageDeleteExecute(TLMessageBase messageBase)
        {
            if (messageBase == null) return;

            var message = messageBase as TLMessage;
            if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            {
                var dialog = new DeleteChannelMessageDialog();

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var channel = With as TLChannel;

                    if (dialog.DeleteAll)
                    {
                        // TODO
                    }
                    else
                    {
                        var messages = new List<TLMessageBase>() { messageBase };
                        if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                        {
                            DeleteMessagesInternal(null, messages);
                            return;
                        }

                        DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
                    }

                    if (dialog.BanUser)
                    {
                        // TODO: layer 68
                        //var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
                        //if (response.IsSucceeded)
                        //{
                        //    var updates = response.Result as TLUpdates;
                        //    if (updates != null)
                        //    {
                        //        var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                        //        if (newChannelMessageUpdate != null)
                        //        {
                        //            Aggregator.Publish(newChannelMessageUpdate.Message);
                        //        }
                        //    }
                        //}
                    }

                    if (dialog.ReportSpam)
                    {
                        var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Delete";
                dialog.Message = "Do you want to delete this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var chat = With as TLChat;

                if (message != null && (message.IsOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && message.ToId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && message.Date + config.EditTimeLimit > date)
                    {
                        var user = With as TLUser;
                        if (user != null && !user.IsBot)
                        {
                            dialog.CheckBoxLabel = string.Format("Delete for {0}", user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = "Delete for everyone";
                        }
                    }
                }
                else if (Peer is TLInputPeerUser && With is TLUser user && !user.IsSelf)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                }
                else if (Peer is TLInputPeerChat)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                }
                else if (Peer is TLInputPeerChannel)
                {
                    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var messages = new List<TLMessageBase>() { messageBase };
                    if (messageBase.Id == 0 && messageBase.RandomId != 0L)
                    {
                        await TLMessageDialog.ShowAsync("This message has no ID, so it will be deleted locally only.", "Warning", "OK");

                        DeleteMessagesInternal(null, messages);
                        return;
                    }

                    DeleteMessages(null, null, messages, revoke, null, DeleteMessagesInternal);
                }
            }
        }

        private void DeleteMessagesInternal(TLMessageBase lastMessage, IList<TLMessageBase> messages)
        {
            var cachedMessages = new TLVector<long>();
            var remoteMessages = new TLVector<int>();
            for (int i = 0; i < messages.Count; i++)
            {
                if (messages[i].RandomId.HasValue && messages[i].RandomId != 0L)
                {
                    cachedMessages.Add(messages[i].RandomId.Value);
                }
                if (messages[i].Id > 0)
                {
                    remoteMessages.Add(messages[i].Id);
                }
            }

            CacheService.DeleteMessages(Peer.ToPeer(), lastMessage, remoteMessages);
            CacheService.DeleteMessages(cachedMessages);

            BeginOnUIThread(() =>
            {
                for (int j = 0; j < messages.Count; j++)
                {
                    if (EditedMessage?.Id == messages[j].Id)
                    {
                        ClearReplyCommand.Execute();
                    }
                    else if (ReplyInfo?.ReplyToMsgId == messages[j].Id)
                    {
                        ClearReplyCommand.Execute();
                    }

                    Items.Remove(messages[j]);
                }

                RaisePropertyChanged(() => With);
                SelectionMode = ListViewSelectionMode.None;

                //this.IsEmptyDialog = (this.Items.get_Count() == 0 && this.LazyItems.get_Count() == 0);
                //this.NotifyOfPropertyChange<TLObject>(() => this.With);
            });
        }

        public async void DeleteMessages(TLMessageBase lastItem, IList<TLMessageBase> localMessages, IList<TLMessageBase> remoteMessages, bool revoke, Action<TLMessageBase, IList<TLMessageBase>> localCallback = null, Action<TLMessageBase, IList<TLMessageBase>> remoteCallback = null)
        {
            if (localMessages != null && localMessages.Count > 0)
            {
                localCallback?.Invoke(lastItem, localMessages);
            }
            if (remoteMessages != null && remoteMessages.Count > 0)
            {
                var messages = new TLVector<int>(remoteMessages.Select(x => x.Id).ToList());

                Task<MTProtoResponse<TLMessagesAffectedMessages>> task;

                if (Peer is TLInputPeerChannel)
                {
                    task = ProtoService.DeleteMessagesAsync(new TLInputChannel { ChannelId = ((TLInputPeerChannel)Peer).ChannelId, AccessHash = ((TLInputPeerChannel)Peer).AccessHash }, messages);
                }
                else
                {
                    task = ProtoService.DeleteMessagesAsync(messages, revoke);
                }

                var response = await task;
                if (response.IsSucceeded)
                {
                    remoteCallback?.Invoke(lastItem, remoteMessages);
                }
            }
        }

        #endregion

        #region Forward

        public RelayCommand<TLMessageBase> MessageForwardCommand { get; }
        private async void MessageForwardExecute(TLMessageBase message)
        {
            if (message is TLMessage)
            {
                Search = null;
                SelectionMode = ListViewSelectionMode.None;

                await ForwardView.Current.ShowAsync(new List<TLMessage> { message as TLMessage });

                //App.InMemoryState.ForwardMessages = new List<TLMessage> { message as TLMessage };
                //NavigationService.GoBackAt(0);
            }
        }

        #endregion

        #region Share

        public RelayCommand<TLMessage> MessageShareCommand { get; }
        private async void MessageShareExecute(TLMessage message)
        {
            await ShareView.Current.ShowAsync(message);
        }

        #endregion

        #region Multiple Delete

        private RelayCommand _messagesDeleteCommand;
        public RelayCommand MessagesDeleteCommand => _messagesDeleteCommand = (_messagesDeleteCommand ?? new RelayCommand(MessagesDeleteExecute, () => SelectedItems.Count > 0 && SelectedItems.All(messageCommon =>
        {
            var channel = _with as TLChannel;
            if (channel != null)
            {
                if (messageCommon.Id == 1 && messageCommon.ToId is TLPeerChannel)
                {
                    return false;
                }

                if (messageCommon.IsOut || channel.IsCreator || (channel.HasAdminRights && channel.AdminRights.IsDeleteMessages))
                {
                    return true;
                }

                return false;
            }

            return true;
        })));

        private async void MessagesDeleteExecute()
        {
            //if (messageBase == null) return;

            //var message = messageBase as TLMessage;
            //if (message != null && !message.IsOut && !message.IsPost && Peer is TLInputPeerChannel)
            //{
            //    var dialog = new DeleteChannelMessageDialog();

            //    var result = await dialog.ShowAsync();
            //    if (result == ContentDialogResult.Primary)
            //    {
            //        var channel = With as TLChannel;

            //        if (dialog.DeleteAll)
            //        {
            //            // TODO
            //        }
            //        else
            //        {
            //            var messages = new List<TLMessageBase>() { messageBase };
            //            if (messageBase.Id == 0 && messageBase.RandomId != 0L)
            //            {
            //                DeleteMessagesInternal(null, messages);
            //                return;
            //            }

            //            DeleteMessages(null, null, messages, true, null, DeleteMessagesInternal);
            //        }

            //        if (dialog.BanUser)
            //        {
            //            var response = await ProtoService.KickFromChannelAsync(channel, message.From.ToInputUser(), true);
            //            if (response.IsSucceeded)
            //            {
            //                var updates = response.Result as TLUpdates;
            //                if (updates != null)
            //                {
            //                    var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
            //                    if (newChannelMessageUpdate != null)
            //                    {
            //                        Aggregator.Publish(newChannelMessageUpdate.Message);
            //                    }
            //                }
            //            }
            //        }

            //        if (dialog.ReportSpam)
            //        {
            //            var response = await ProtoService.ReportSpamAsync(channel.ToInputChannel(), message.From.ToInputUser(), new TLVector<int> { message.Id });
            //        }
            //    }
            //}
            //else
            {
                var messages = new List<TLMessageCommonBase>(SelectedItems);

                var dialog = new TLMessageDialog();
                dialog.Title = "Delete";
                dialog.Message = messages.Count > 1 ? string.Format("Do you want to delete this {0} messages?", messages.Count) : "Do you want to delete this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var chat = With as TLChat;

                var isOut = messages.All(x => x.IsOut);
                var toId = messages.FirstOrDefault().ToId;
                var minDate = messages.OrderBy(x => x.Date).FirstOrDefault().Date;
                var maxDate = messages.OrderByDescending(x => x.Date).FirstOrDefault().Date;

                if ((isOut || (chat != null && (chat.IsCreator || chat.IsAdmin))) && toId.Id != SettingsHelper.UserId && (Peer is TLInputPeerUser || Peer is TLInputPeerChat))
                {
                    var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);
                    var config = CacheService.GetConfig();
                    if (config != null && minDate + config.EditTimeLimit > date && maxDate + config.EditTimeLimit > date)
                    {
                        var user = With as TLUser;
                        if (user != null && !user.IsBot)
                        {
                            dialog.CheckBoxLabel = string.Format("Delete for {0}", user.FullName);
                        }

                        //var chat = With as TLChat;
                        if (chat != null)
                        {
                            dialog.CheckBoxLabel = "Delete for everyone";
                        }
                    }
                }
                else if (Peer is TLInputPeerUser && With is TLUser user && !user.IsSelf)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you.";
                }
                else if (Peer is TLInputPeerChat)
                {
                    dialog.Message += "\r\n\r\nThis will delete it just for you, not for other participants of the chat.";
                }
                else if (Peer is TLInputPeerChannel)
                {
                    dialog.Message += "\r\n\r\nThis will delete it for everyone in this chat.";
                }

                var result = await dialog.ShowQueuedAsync();
                if (result == ContentDialogResult.Primary)
                {
                    var revoke = dialog.IsChecked == true;

                    var localMessages = new List<TLMessageBase>();
                    var remoteMessages = new List<TLMessageBase>();
                    for (int i = 0; i < messages.Count; i++)
                    {
                        var message = messages[i];
                        if (message.Id == 0 && message.RandomId != 0L)
                        {
                            localMessages.Add(message);
                        }
                        else if (message.Id != 0)
                        {
                            remoteMessages.Add(message);
                        }
                    }

                    DeleteMessages(null, localMessages, remoteMessages, revoke, DeleteMessagesInternal, DeleteMessagesInternal);
                }
            }
        }

        #endregion

        #region Multiple Forward

        private RelayCommand _messagesForwardCommand;
        public RelayCommand MessagesForwardCommand => _messagesForwardCommand = (_messagesForwardCommand ?? new RelayCommand(MessagesForwardExecute, () => SelectedItems.Count > 0 && SelectedItems.All(x =>
        {
            if (x is TLMessage message)
            {
                if (message.Media is TLMessageMediaPhoto photoMedia)
                {
                    return !photoMedia.HasTTLSeconds;
                }
                else if (message.Media is TLMessageMediaDocument documentMedia)
                {
                    return !documentMedia.HasTTLSeconds;
                }

                return true;
            }

            return false;
        })));

        private async void MessagesForwardExecute()
        {
            var messages = SelectedItems.OfType<TLMessage>().Where(x => x.Id != 0).OrderBy(x => x.Id).ToList();
            if (messages.Count > 0)
            {
                Search = null;
                SelectionMode = ListViewSelectionMode.None;

                await ForwardView.Current.ShowAsync(messages);

                //App.InMemoryState.ForwardMessages = new List<TLMessage>(messages);
                //NavigationService.GoBackAt(0);
            }
        }

        #endregion

        #region Select

        public RelayCommand<TLMessageBase> MessageSelectCommand { get; }
        private void MessageSelectExecute(TLMessageBase message)
        {
            Search = null;

            var messageCommon = message as TLMessageCommonBase;
            if (messageCommon == null)
            {
                return;
            }

            SelectionMode = ListViewSelectionMode.Multiple;

            SelectedItems = new List<TLMessageCommonBase> { messageCommon };
            RaisePropertyChanged("SelectedItems");
        }

        #endregion

        #region Copy

        public RelayCommand<TLMessage> MessageCopyCommand { get; }
        private void MessageCopyExecute(TLMessage message)
        {
            if (message == null)
            {
                return;
            }

            string text = null;

            var media = message.Media as ITLMessageMediaCaption;
            if (media != null && !string.IsNullOrWhiteSpace(media.Caption))
            {
                text = media.Caption;
            }
            else if (!string.IsNullOrWhiteSpace(message.Message))
            {
                text = message.Message;
            }

            if (text != null)
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(text);
                ClipboardEx.TrySetContent(dataPackage);
            }
        }

        #endregion

        #region Copy media

        public RelayCommand<TLMessage> MessageCopyMediaCommand { get; }
        private async void MessageCopyMediaExecute(TLMessage message)
        {
            var photo = message.GetPhoto();
            var photoSize = photo?.Full as TLPhotoSize;
            if (photoSize == null)
            {
                return;
            }

            var location = photoSize.Location;
            var fileName = string.Format("{0}_{1}_{2}.jpg", location.VolumeId, location.LocalId, location.Secret);
            if (File.Exists(FileUtils.GetTempFileName(fileName)))
            {
                var result = await FileUtils.GetTempFileAsync(fileName);

                try
                {
                    var dataPackage = new DataPackage();
                    dataPackage.SetStorageItems(new[] { result });
                    ClipboardEx.TrySetContent(dataPackage);
                }
                catch { }
            }
        }

        #endregion

        #region Copy link

        public RelayCommand<TLMessageCommonBase> MessageCopyLinkCommand { get; }
        private void MessageCopyLinkExecute(TLMessageCommonBase messageCommon)
        {
            if (messageCommon == null)
            {
                return;
            }

            var channel = With as TLChannel;
            if (channel == null)
            {
                return;
            }

            var link = $"{channel.Username}/{messageCommon.Id}";

            if (messageCommon is TLMessage message && message.IsRoundVideo())
            {
                link = $"https://telesco.pe/{link}";
            }
            else
            {
                link = UsernameToLinkConverter.Convert(link);
            }

            var dataPackage = new DataPackage();
            dataPackage.SetText(link);
            ClipboardEx.TrySetContent(dataPackage);
        }

        #endregion

        #region Edit

        public RelayCommand MessageEditLastCommand { get; }
        private void MessageEditLastExecute()
        {
            var last = Items.LastOrDefault(x => x is TLMessage message && message.IsOut);
            if (last != null)
            {
                MessageEditCommand.Execute(last);
            }
        }

        public RelayCommand<TLMessage> MessageEditCommand { get; }
        private async void MessageEditExecute(TLMessage message)
        {
            Search = null;

            if (message == null)
            {
                return;
            }

            var response = await ProtoService.GetMessageEditDataAsync(Peer, message.Id);
            if (response.IsSucceeded)
            {
                BeginOnUIThread(() =>
                {
                    var messageEditText = GetMessageEditText(response.Result, message);
                    StartEditMessage(messageEditText, message);
                });
            }
            else
            {
                BeginOnUIThread(() =>
                {
                    //this.IsWorking = false;
                    //if (error.CodeEquals(ErrorCode.BAD_REQUEST) && error.TypeEquals(ErrorType.MESSAGE_ID_INVALID))
                    //{
                    //    MessageBox.Show(AppResources.EditMessageError, AppResources.Error, 0);
                    //    return;
                    //}
                    Execute.ShowDebugMessage("messages.getMessageEditData error " + response.Error);
                });
            }
        }

        public void StartEditMessage(string text, TLMessage message)
        {
            if (text == null)
            {
                return;
            }
            if (message == null)
            {
                return;
            }

            var config = CacheService.GetConfig();
            var editUntil = (config != null) ? (message.Date + config.EditTimeLimit + 300) : 0;
            if (message.FromId != null && message.ToId is TLPeerUser && message.FromId.Value == message.ToId.Id)
            {
                editUntil = 0;
            }

            Reply = new TLMessagesContainter
            {
                EditMessage = message,
                EditUntil = editUntil,
                // TODO: setup original content
                PreviousMessage = new TLMessage
                {
                    ToId = message.ToId,
                    FromId = message.FromId,
                    IsOut = message.IsOut
                }
            };

            SetText(text, message.Entities, true);

            //if (this._editMessageTimer == null)
            //{
            //    this._editMessageTimer = new DispatcherTimer();
            //    this._editMessageTimer.add_Tick(new EventHandler(this.OnEditMessageTimerTick));
            //    this._editMessageTimer.set_Interval(System.TimeSpan.FromSeconds(1.0));
            //}
            //this._editMessageTimer.Start();
            //this.IsEditingEnabled = true;
            //this.Text = text.ToString();

            CurrentInlineBot = null;

            //this.ClearStickerHints();
            //this.ClearInlineBotResults();
            //this.ClearUsernameHints();
            //this.ClearHashtagHints();
            //this.ClearCommandHints();
        }

        private string GetMessageEditText(TLMessagesMessageEditData editData, TLMessage message)
        {
            if (editData.IsCaption)
            {
                var mediaCaption = message.Media as ITLMessageMediaCaption;
                if (mediaCaption != null)
                {
                    return mediaCaption.Caption ?? string.Empty;
                }
            }
            else
            {
                return message.Message;
            }

            return null;

            //if (!editData.IsCaption)
            //{
            //    var text = message.Message.ToString();
            //    var stringBuilder = new StringBuilder();

            //    if (message != null && message.Entities != null && message.Entities.Count > 0)
            //    {
            //        //this.ClearMentions();

            //        if (message.Entities.FirstOrDefault(x => !(x is TLMessageEntityMentionName) && !(x is TLInputMessageEntityMentionName)) == null)
            //        {
            //            for (int i = 0; i < message.Entities.Count; i++)
            //            {
            //                int num = (i == 0) ? 0 : (message.Entities[i - 1].Offset + message.Entities[i - 1].Length);
            //                int num2 = (i == 0) ? message.Entities[i].Offset : (message.Entities[i].Offset - num);

            //                stringBuilder.Append(text.Substring(num, num2));

            //                var entityMentionName = message.Entities[i] as TLMessageEntityMentionName;
            //                if (entityMentionName != null)
            //                {
            //                    var user = CacheService.GetUser(entityMentionName.UserId);
            //                    if (user != null)
            //                    {
            //                        //this.AddMention(user);
            //                        string text2 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                        stringBuilder.Append(string.Format("@({0})", text2));
            //                    }
            //                }
            //                else
            //                {
            //                    var entityInputMentionName = message.Entities[i] as TLInputMessageEntityMentionName;
            //                    if (entityInputMentionName != null)
            //                    {
            //                        var inputUser = entityInputMentionName.UserId as TLInputUser;
            //                        if (inputUser != null)
            //                        {
            //                            TLUserBase user2 = this.CacheService.GetUser(inputUser.UserId);
            //                            if (user2 != null)
            //                            {
            //                                //this.AddMention(user2);
            //                                string text3 = text.Substring(message.Entities[i].Offset, message.Entities[i].Length);
            //                                stringBuilder.Append(string.Format("@({0})", text3));
            //                            }
            //                        }
            //                    }
            //                    else
            //                    {
            //                        num = message.Entities[i].Offset;
            //                        num2 = message.Entities[i].Length;
            //                        stringBuilder.Append(text.Substring(num, num2));
            //                    }
            //                }
            //            }

            //            var baseEntity = message.Entities[message.Entities.Count - 1];
            //            if (baseEntity != null)
            //            {
            //                stringBuilder.Append(text.Substring(baseEntity.Offset + baseEntity.Length));
            //            }
            //        }
            //        else
            //        {
            //            stringBuilder.Append(text);
            //        }
            //    }
            //    else
            //    {
            //        stringBuilder.Append(text);
            //    }

            //    return stringBuilder.ToString();
            //}

            //var mediaCaption = message.Media as ITLMediaCaption;
            //if (mediaCaption != null)
            //{
            //    return mediaCaption.Caption;
            //}

            //return null;
        }

        #endregion

        #region Pin

        public RelayCommand<TLMessageBase> MessagePinCommand { get; }
        private async void MessagePinExecute(TLMessageBase message)
        {
            if (PinnedMessage?.Id == message.Id)
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Unpin message";
                dialog.Message = "Would you like to unpin this message?";
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var dialogResult = await dialog.ShowQueuedAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var channel = Peer as TLInputPeerChannel;
                    var inputChannel = new TLInputChannel { ChannelId = channel.ChannelId, AccessHash = channel.AccessHash };

                    var result = await ProtoService.UpdatePinnedMessageAsync(false, inputChannel, 0);
                    if (result.IsSucceeded)
                    {
                        PinnedMessage = null;
                    }
                }
            }
            else
            {
                var dialog = new TLMessageDialog();
                dialog.Title = "Pin message";
                dialog.Message = "Would you like to pin this message?";
                dialog.CheckBoxLabel = "Notify all members";
                dialog.IsChecked = true;
                dialog.PrimaryButtonText = "Yes";
                dialog.SecondaryButtonText = "No";

                var dialogResult = await dialog.ShowQueuedAsync();
                if (dialogResult == ContentDialogResult.Primary)
                {
                    var channel = Peer as TLInputPeerChannel;
                    var inputChannel = new TLInputChannel { ChannelId = channel.ChannelId, AccessHash = channel.AccessHash };

                    var silent = dialog.IsChecked == false;
                    var result = await ProtoService.UpdatePinnedMessageAsync(silent, inputChannel, message.Id);
                    if (result.IsSucceeded)
                    {
                        var updates = result.Result as TLUpdates;
                        if (updates != null)
                        {
                            var newChannelMessageUpdate = updates.Updates.OfType<TLUpdateNewChannelMessage>().FirstOrDefault();
                            if (newChannelMessageUpdate != null)
                            {
                                Handle(newChannelMessageUpdate.Message as TLMessageCommonBase);
                                Aggregator.Publish(new TopMessageUpdatedEventArgs(_dialog, newChannelMessageUpdate.Message));
                            }
                        }

                        PinnedMessage = message;
                    }
                }
            }
        }

        #endregion

        #region Keyboard button

        private TLMessage _replyMarkupMessage;
        private TLReplyMarkupBase _replyMarkup;

        public TLMessage EditedMessage
        {
            get
            {
                if (Reply is TLMessagesContainter container)
                {
                    return container.EditMessage;
                }

                return null;
            }
        }

        public TLReplyMarkupBase ReplyMarkup
        {
            get
            {
                return _replyMarkup;
            }
            set
            {
                Set(ref _replyMarkup, value);
            }
        }

        private void SetReplyMarkup(TLMessage message)
        {
            if (Reply != null && message != null)
            {
                return;
            }

            if (message != null && message.ReplyMarkup != null)
            {
                if (message.ReplyMarkup is TLReplyInlineMarkup)
                {
                    return;
                }

                //var keyboardMarkup = message.ReplyMarkup as TLReplyKeyboardMarkup;
                //if (keyboardMarkup != null && keyboardMarkup.IsPersonal && !message.IsMention)
                //{
                //    return;
                //}

                var keyboardHide = message.ReplyMarkup as TLReplyKeyboardHide;
                if (keyboardHide != null && _replyMarkupMessage != null && _replyMarkupMessage.FromId.Value != message.FromId.Value)
                {
                    return;
                }

                var keyboardForceReply = message.ReplyMarkup as TLReplyKeyboardForceReply;
                if (keyboardForceReply != null /*&& !keyboardForceReply.HasResponse*/)
                {
                    _replyMarkupMessage = null;
                    ReplyMarkup = null;
                    Reply = message;
                    return;
                }

            }

            if (_replyMarkupMessage != null && _replyMarkupMessage.Id > message.Id)
            {
                return;
            }

            //this.SuppressOpenCommandsKeyboard = (message != null && message.ReplyMarkup != null && suppressOpenKeyboard);

            _replyMarkupMessage = message;
            ReplyMarkup = message.ReplyMarkup;
        }

        //public RelayCommand<TLKeyboardButtonBase> KeyboardButtonCommand { get; }
        public async void KeyboardButtonExecute(TLKeyboardButtonBase button, TLMessage message)
        {
            if (button is TLKeyboardButtonBuy buyButton)
            {
                if (message.Media is TLMessageMediaInvoice invoiceMedia && invoiceMedia.HasReceiptMsgId)
                {
                    var response = await ProtoService.GetPaymentReceiptAsync(invoiceMedia.ReceiptMsgId.Value);
                    if (response.IsSucceeded)
                    {
                        NavigationService.Navigate(typeof(PaymentReceiptPage), TLTuple.Create(message, response.Result));
                    }
                }
                else
                {
                    var response = await ProtoService.GetPaymentFormAsync(message.Id);
                    if (response.IsSucceeded)
                    {
                        if (response.Result.Invoice.IsEmailRequested || response.Result.Invoice.IsNameRequested || response.Result.Invoice.IsPhoneRequested || response.Result.Invoice.IsShippingAddressRequested)
                        {
                            NavigationService.NavigateToPaymentFormStep1(message, response.Result);
                        }
                        else if (response.Result.HasSavedCredentials)
                        {
                            if (ApplicationSettings.Current.TmpPassword != null)
                            {
                                if (ApplicationSettings.Current.TmpPassword.ValidUntil < TLUtils.Now + 60)
                                {
                                    ApplicationSettings.Current.TmpPassword = null;
                                }
                            }

                            if (ApplicationSettings.Current.TmpPassword != null)
                            {
                                NavigationService.NavigateToPaymentFormStep5(message, response.Result, null, null, null, null, null, true);
                            }
                            else
                            {
                                NavigationService.NavigateToPaymentFormStep4(message, response.Result, null, null, null);
                            }
                        }
                        else
                        {
                            NavigationService.NavigateToPaymentFormStep3(message, response.Result, null, null, null);
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonSwitchInline switchInlineButton)
            {
                var bot = GetBot(message);
                if (bot != null)
                {
                    if (switchInlineButton.IsSamePeer)
                    {
                        SetText(string.Format("@{0} {1}", bot.Username, switchInlineButton.Query), focus: true);
                        ResolveInlineBot(bot.Username, switchInlineButton.Query);

                        if (With is TLChatBase)
                        {
                            Reply = message;
                        }
                    }
                    else
                    {
                        await ForwardView.Current.ShowAsync(switchInlineButton, bot);
                    }
                }
            }
            else if (button is TLKeyboardButtonUrl urlButton)
            {
                var url = urlButton.Url;
                if (url.StartsWith("http") == false)
                {
                    url = "http://" + url;
                }

                if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                {
                    if (MessageHelper.IsTelegramUrl(uri))
                    {
                        MessageHelper.HandleTelegramUrl(urlButton.Url);
                    }
                    else
                    {
                        var dialog = new TLMessageDialog(urlButton.Url, "Open this link?");
                        dialog.PrimaryButtonText = "OK";
                        dialog.SecondaryButtonText = "Cancel";

                        var result = await dialog.ShowQueuedAsync();
                        if (result != ContentDialogResult.Primary)
                        {
                            return;
                        }

                        await Launcher.LaunchUriAsync(uri);
                    }
                }
            }
            else if (button is TLKeyboardButtonCallback callbackButton)
            {
                var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, callbackButton.Data, false);
                if (response.IsSucceeded)
                {
                    if (response.Result.HasMessage)
                    {
                        if (response.Result.IsAlert)
                        {
                            await new TLMessageDialog(response.Result.Message).ShowQueuedAsync();
                        }
                        else
                        {
                            var date = TLUtils.DateToUniversalTimeTLInt(ProtoService.ClientTicksDelta, DateTime.Now);

                            var bot = GetBot(message);
                            if (bot == null)
                            {
                                // TODO:
                                await new TLMessageDialog(response.Result.Message).ShowQueuedAsync();
                                return;
                            }

                            InformativeMessage = TLUtils.GetShortMessage(0, bot.Id, Peer.ToPeer(), date, response.Result.Message);
                        }
                    }
                    else if (response.Result.HasUrl && response.Result.IsHasUrl /* ??? */)
                    {
                        var url = response.Result.Url;
                        if (url.StartsWith("http") == false)
                        {
                            url = "http://" + url;
                        }

                        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
                        {
                            if (MessageHelper.IsTelegramUrl(uri))
                            {
                                MessageHelper.HandleTelegramUrl(response.Result.Url);
                            }
                            else
                            {
                                //var dialog = new TLMessageDialog(response.Result.Url, "Open this link?");
                                //dialog.PrimaryButtonText = "OK";
                                //dialog.SecondaryButtonText = "Cancel";

                                //var result = await dialog.ShowQueuedAsync();
                                //if (result != ContentDialogResult.Primary)
                                //{
                                //    return;
                                //}

                                await Launcher.LaunchUriAsync(uri);
                            }
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonGame gameButton)
            {
                var gameMedia = message.Media as TLMessageMediaGame;
                if (gameMedia != null)
                {
                    var response = await ProtoService.GetBotCallbackAnswerAsync(Peer, message.Id, null, true);
                    if (response.IsSucceeded && response.Result.IsHasUrl && response.Result.HasUrl)
                    {
                        if (CacheService.GetUser(message.ViaBotId) is TLUser user)
                        {
                            NavigationService.Navigate(typeof(GamePage), new TLTuple<string, string, string, TLMessage>(gameMedia.Game.Title, user.Username, response.Result.Url, message));
                        }
                        else
                        {
                            NavigationService.Navigate(typeof(GamePage), new TLTuple<string, string, string, TLMessage>(gameMedia.Game.Title, string.Empty, response.Result.Url, message));
                        }
                    }
                }
            }
            else if (button is TLKeyboardButtonRequestPhone requestPhoneButton)
            {
                if (CacheService.GetUser(SettingsHelper.UserId) is TLUser cached)
                {
                    var confirm = await TLMessageDialog.ShowAsync("The bot will know your phone number. This can be useful for integration with other services.", "Share your phone number?", "OK", "Cancel");
                    if (confirm == ContentDialogResult.Primary)
                    {
                        await SendContactAsync(cached);
                    }
                }
            }
            else if (button is TLKeyboardButtonRequestGeoLocation requestGeoButton)
            {
                var confirm = await TLMessageDialog.ShowAsync("This will send your current location to the bot.", "Share your location?", "OK", "Cancel");
                if (confirm == ContentDialogResult.Primary)
                {
                    var location = await _locationService.GetPositionAsync();
                    if (location != null)
                    {
                        await SendGeoAsync(location.Point.Position.Latitude, location.Point.Position.Longitude);
                    }
                }
            }
            else if (button is TLKeyboardButton keyboardButton)
            {
                await SendMessageAsync(keyboardButton.Text, null, true);
            }
        }

        #endregion

        #region Open reply

        public RelayCommand<TLMessageCommonBase> MessageOpenReplyCommand { get; }
        private async void MessageOpenReplyExecute(TLMessageCommonBase messageCommon)
        {
            if (messageCommon != null && messageCommon.ReplyToMsgId.HasValue)
            {
                await LoadMessageSliceAsync(messageCommon.Id, messageCommon.ReplyToMsgId.Value);
            }
        }

        #endregion

        #region Sticker info

        public RelayCommand<TLMessage> MessageStickerPackInfoCommand { get; }
        private async void MessageStickerPackInfoExecute(TLMessage message)
        {
            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var stickerAttribute = document.Attributes.OfType<TLDocumentAttributeSticker>().FirstOrDefault();
                if (stickerAttribute != null && stickerAttribute.StickerSet.TypeId != TLType.InputStickerSetEmpty)
                {
                    await StickerSetView.Current.ShowAsync(stickerAttribute.StickerSet);
                }
            }
        }

        #endregion

        #region Fave sticker

        public RelayCommand<TLMessage> MessageFaveStickerCommand { get; }
        private void MessageFaveStickerExecute(TLMessage message)
        {
            if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                _stickersService.AddRecentSticker(StickerType.Fave, document, (int)(Utils.CurrentTimestamp / 1000), false);
            }
        }

        #endregion

        #region Unfave sticker

        public RelayCommand<TLMessage> MessageUnfaveStickerCommand { get; }
        private void MessageUnfaveStickerExecute(TLMessage message)
        {
            if (message.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                _stickersService.AddRecentSticker(StickerType.Fave, document, (int)(Utils.CurrentTimestamp / 1000), true);
            }
        }

        #endregion

        #region Save sticker as

        public RelayCommand<TLMessage> MessageSaveStickerCommand { get; }
        private async void MessageSaveStickerExecute(TLMessage message)
        {
            if (message?.Media is TLMessageMediaDocument documentMedia && documentMedia.Document is TLDocument document)
            {
                var fileName = document.GetFileName();
                if (File.Exists(FileUtils.GetTempFileName(fileName)))
                {
                    var picker = new FileSavePicker();
                    picker.FileTypeChoices.Add("WebP image", new[] { ".webp" });
                    picker.FileTypeChoices.Add("PNG image", new[] { ".png" });
                    picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    picker.SuggestedFileName = "sticker.webp";

                    var fileNameAttribute = document.Attributes.OfType<TLDocumentAttributeFilename>().FirstOrDefault();
                    if (fileNameAttribute != null)
                    {
                        picker.SuggestedFileName = fileNameAttribute.FileName;
                    }

                    var file = await picker.PickSaveFileAsync();
                    if (file != null)
                    {
                        var sticker = await FileUtils.GetTempFileAsync(fileName);

                        if (Path.GetExtension(file.Name).Equals(".webp"))
                        {
                            await sticker.CopyAndReplaceAsync(file);
                        }
                        else if (Path.GetExtension(file.Name).Equals(".png"))
                        {
                            var buffer = await FileIO.ReadBufferAsync(sticker);
                            var bitmap = WebPImage.DecodeFromBuffer(buffer);

                            using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                            {
                                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                                var pixelStream = bitmap.PixelBuffer.AsStream();
                                var pixels = new byte[pixelStream.Length];

                                await pixelStream.ReadAsync(pixels, 0, pixels.Length);

                                encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, (uint)bitmap.PixelWidth, (uint)bitmap.PixelHeight, 96.0, 96.0, pixels);
                                await encoder.FlushAsync();
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Save file as

        public RelayCommand<TLMessage> MessageSaveMediaCommand { get; }
        private async void MessageSaveMediaExecute(TLMessage message)
        {
            if (message.IsSticker())
            {
                MessageSaveStickerExecute(message);
                return;
            }

            var photo = message.GetPhoto();
            if (photo?.Full is TLPhotoSize photoSize)
            {
                await TLFileHelper.SavePhotoAsync(photoSize, message.Date);
            }

            var document = message.GetDocument();
            if (document != null)
            {
                await TLFileHelper.SaveDocumentAsync(document, message.Date);
            }
        }

        #endregion

        #region Save to GIFs

        public RelayCommand<TLMessage> MessageSaveGIFCommand { get; }
        private async void MessageSaveGIFExecute(TLMessage message)
        {
            TLDocument document = null;
            if (message?.Media is TLMessageMediaDocument documentMedia)
            {
                document = documentMedia.Document as TLDocument;
            }
            else if (message?.Media is TLMessageMediaWebPage webPageMedia && webPageMedia.WebPage is TLWebPage webPage)
            {
                document = webPage.Document as TLDocument;
            }

            if (document == null)
            {
                return;
            }

            var response = await ProtoService.SaveGifAsync(new TLInputDocument { Id = document.Id, AccessHash = document.AccessHash }, false);
            if (response.IsSucceeded)
            {
                _stickers.StickersService.AddRecentGif(document, (int)(Utils.CurrentTimestamp / 1000));
            }
        }

        #endregion
    }
}
