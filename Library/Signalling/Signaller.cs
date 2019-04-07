﻿using Newtonsoft.Json;
using Org.WebRtc;
using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using MessageType = ApolloLensLibrary.Signalling.MessageProtocol.MessageType;

namespace ApolloLensLibrary.Signalling
{
    public class WebSocketSignaller : IClientSignaller, ISourceSignaller, IUISignaller
    {
        public event EventHandler<RTCSessionDescription> ReceivedOffer;
        public event EventHandler<RTCSessionDescription> ReceivedAnswer;
        public event EventHandler<RTCIceCandidate> ReceivedIceCandidate;
        public event EventHandler<string> ReceivedPlainMessage;
        public event EventHandler ReceivedShutdown;
        public event EventHandler ConnectionFailed;

        private MessageWebSocket WebSocket { get; set; }

        public static IUISignaller CreateSignaller()
        {
            return new WebSocketSignaller();
        }

        public async Task ConnectToServer(string address)
        {
            try
            {
                this.WebSocket = new MessageWebSocket();
                this.WebSocket.Control.MessageType = SocketMessageType.Utf8;
                this.WebSocket.MessageReceived += this.WebSocket_MessageReceived;
                this.WebSocket.Closed += this.WebSocket_Closed;
                await this.WebSocket.ConnectAsync(new Uri(address));
            }
            catch
            {
                this.ConnectionFailed?.Invoke(this, EventArgs.Empty);
            }
        }

        public void DisconnectFromServer()
        {
            this.WebSocket.Close(1000, "");
        }        

        public async Task SendPlainMessage(string messageContents)
        {
            await this.SendMessage(messageContents, MessageType.Plain);
        }

        public async Task SendOffer(RTCSessionDescription offer)
        {
            var messageContents = JsonConvert.SerializeObject(offer);
            await this.SendMessage(messageContents, MessageType.Offer);
        }

        public async Task SendAnswer(RTCSessionDescription answer)
        {
            var messageContents = JsonConvert.SerializeObject(answer);
            await this.SendMessage(messageContents, MessageType.Answer);
        }

        public async Task SendIceCandidate(RTCIceCandidate iceCandidate)
        {
            var messageContents = JsonConvert.SerializeObject(iceCandidate);
            await this.SendMessage(messageContents, MessageType.IceCandidate);
        }

        public async Task SendShutdown()
        {
            await this.SendMessage("", MessageType.Shutdown);
        }

        private async Task SendMessage(string message, MessageType messageType)
        {
            var wrappedMessage = MessageProtocol.WrapMessage(message, messageType);

            using (var dataWriter = new DataWriter(this.WebSocket.OutputStream))
            {
                dataWriter.WriteString(wrappedMessage);
                await dataWriter.StoreAsync();
                dataWriter.DetachStream();
            }
        }

        private void WebSocket_MessageReceived(MessageWebSocket sender, MessageWebSocketMessageReceivedEventArgs args)
        {
            try
            {
                using (DataReader dataReader = args.GetDataReader())
                {
                    dataReader.UnicodeEncoding = UnicodeEncoding.Utf8;
                    var rawMessage = dataReader.ReadString(dataReader.UnconsumedBufferLength);
                    var message = MessageProtocol.UnwrapMessage(rawMessage);

                    switch (message.Type)
                    {
                        case MessageType.Answer:
                            {
                                var answer = JsonConvert.DeserializeObject<RTCSessionDescription>(message.Contents);
                                this.ReceivedAnswer?.Invoke(this, answer);
                                break;
                            }
                        case MessageType.Offer:
                            {
                                var offer = JsonConvert.DeserializeObject<RTCSessionDescription>(message.Contents);
                                this.ReceivedOffer?.Invoke(this, offer);
                                break;
                            }
                        case MessageType.IceCandidate:
                            {
                                var candidate = JsonConvert.DeserializeObject<RTCIceCandidate>(message.Contents);
                                this.ReceivedIceCandidate?.Invoke(this, candidate);
                                break;
                            }
                        case MessageType.Plain:
                            {
                                this.ReceivedPlainMessage(this, message.Contents);
                                break;
                            }
                        case MessageType.Shutdown:
                            {
                                this.ReceivedShutdown?.Invoke(this, EventArgs.Empty);
                                break;
                            }
                    }
                }
            }
            catch (Exception ex)
            {
                Windows.Web.WebErrorStatus webErrorStatus = WebSocketError.GetStatus(ex.GetBaseException().HResult);
            }
        }

        private void WebSocket_Closed(IWebSocket sender, WebSocketClosedEventArgs args)
        {
            this.WebSocket.Dispose();
        }
    }

    public interface IBaseSignaller
    {
        Task ConnectToServer(string address);
        void DisconnectFromServer();

        Task SendIceCandidate(RTCIceCandidate iceCandidate);
        event EventHandler<RTCIceCandidate> ReceivedIceCandidate;
    }

    public interface IClientSignaller : IBaseSignaller
    {
        Task SendOffer(RTCSessionDescription offer);
        event EventHandler<RTCSessionDescription> ReceivedAnswer;
    }

    public interface ISourceSignaller : IBaseSignaller
    {
        Task SendAnswer(RTCSessionDescription answer);
        event EventHandler<RTCSessionDescription> ReceivedOffer;
    }

    public interface IUISignaller
    {        
        Task SendPlainMessage(string message);
        Task SendShutdown();

        event EventHandler<string> ReceivedPlainMessage;
        event EventHandler ReceivedShutdown;
        event EventHandler ConnectionFailed;
    }
}
