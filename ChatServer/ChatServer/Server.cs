using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

using System.Net.Sockets;
using System.Net;
using System.Collections;

using ChatApplication;
using System.IO;

namespace Server
{
    public partial class Server : Form
    {
        #region Private Members

        private List<byte[]> outputData = new List<byte[]>();
        private static int messageSize = 1000;
        private static int packetSize = 1024;
        private int currentSeqNum = -1;
        private bool correctSeqRcvd = false;

        // Structure to store the client information
        private struct Client
        {
            public EndPoint endPoint;
        }
        private ArrayList clientList;
        private Socket serverSocket;

        // Used to hold packet data
        private byte[] dataStream = new byte[packetSize];

        //Updates visual display components of application
        private delegate void UpdateStatusDelegate(string status);
        private UpdateStatusDelegate updateStatusDelegate = null;

        #endregion

        #region Constructor

        public Server()
        {
            InitializeComponent();
        }

        #endregion

        #region Events

        private void Server_Load(object sender, EventArgs e)
        {
            try
            {
                this.clientList = new ArrayList();
                this.updateStatusDelegate = new UpdateStatusDelegate(this.UpdateStatus);
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                IPEndPoint server = new IPEndPoint(IPAddress.Any, 30000);
                serverSocket.Bind(server);
                IPEndPoint clients = new IPEndPoint(IPAddress.Any, 0);
                EndPoint epSender = (EndPoint)clients;

                // Listen for packets
                serverSocket.BeginReceiveFrom(this.dataStream, 0, this.dataStream.Length, SocketFlags.None, ref epSender, new AsyncCallback(ReceiveData), epSender);

                lblStatus.Text = "Connected. Listening for incoming packets.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error";
                MessageBox.Show("Load Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Close();
        }

        #endregion

        #region Send And Receive

        public void SendData(IAsyncResult asyncResult)
        {
            try
            {
                serverSocket.EndSend(asyncResult);
            }
            catch (Exception ex)
            {
                MessageBox.Show("SendData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ReceiveData(IAsyncResult asyncResult)
        {
            try
            {
                byte[] data;

                
                // Initialise a packet to store the received data
                Packet receivedData = new Packet(this.dataStream);

                correctSeqRcvd = false;
                if(Convert.ToInt32(receivedData.SequenceNum) == currentSeqNum+1)
                {
                    currentSeqNum++;
                    correctSeqRcvd = true;
                }

                // Initialise a packet to store the data to be sent
                Packet sendData = new Packet();

                IPEndPoint clients = new IPEndPoint(IPAddress.Any, 0);
                EndPoint epSender = (EndPoint)clients;

                // Receive data
                serverSocket.EndReceiveFrom(asyncResult, ref epSender);

                sendData.ClientDataIdentifier = receivedData.ClientDataIdentifier;

                switch (receivedData.ClientDataIdentifier)
                {
                    case DataIdentifier.Message:
                        int ack = 1;
                        sendData.Acknowledgement = ack.ToString();
                        //sendData.Algorithm = receivedData.Algorithm;
                        //sendData.WindowSize = receivedData.WindowSize;
                        sendData.SequenceNum = receivedData.SequenceNum;
                        break;

                    case DataIdentifier.LogIn:
                        Client client = new Client();
                        client.endPoint = epSender;
                        this.clientList.Add(client);

                        String msg = "User Logged In";
                        sendData.Message = Encoding.UTF8.GetBytes(msg);
                        break;

                    case DataIdentifier.LogOut:
                        double percentageLost = BitConverter.ToDouble(receivedData.Message,0);
                        Console.WriteLine(percentageLost);
                        ack = 0;
                        sendData.Acknowledgement = ack.ToString();
                        sendData.Algorithm = receivedData.Algorithm;
                        sendData.WindowSize = receivedData.WindowSize;
                        sendData.SequenceNum = receivedData.SequenceNum;
                        sendData.Message = receivedData.Message;
                        break;

                }
                

                // Get packet as byte array
                data = sendData.GetDataStream();

                if (correctSeqRcvd || receivedData.SequenceNum == "0")
                {
                    foreach (Client client in this.clientList)
                    {
                        if (client.endPoint != epSender /*|| sendData.ClientDataIdentifier != DataIdentifier.LogIn*/)
                        {
                            serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, client.endPoint, new AsyncCallback(this.SendData), client.endPoint);
                        }
                    }

                    if (receivedData.ClientDataIdentifier == DataIdentifier.Message)
                    {
                        //Write to output file
                        using (StreamWriter sw = File.AppendText("output.txt"))
                        {
                            sw.WriteLine(System.Text.Encoding.Default.GetString(receivedData.Message));
                        }

                    }
                }
                else
                {
                    foreach (Client client in this.clientList)
                    {
                        if (client.endPoint != epSender /*|| sendData.ClientDataIdentifier != DataIdentifier.LogIn*/)
                        {
                            sendData.SequenceNum = currentSeqNum.ToString();
                            data = sendData.GetDataStream();
                            serverSocket.BeginSendTo(data, 0, data.Length, SocketFlags.None, client.endPoint, new AsyncCallback(this.SendData), client.endPoint);
                        }
                    }
                }

                serverSocket.BeginReceiveFrom(this.dataStream, 0, this.dataStream.Length, SocketFlags.None, ref epSender, new AsyncCallback(this.ReceiveData), epSender);

                if(sendData.Algorithm == "0")
                    this.Invoke(this.updateStatusDelegate, new object[] { System.Text.Encoding.Default.GetString(sendData.Message) });
            }
            catch (Exception ex)
            {
                MessageBox.Show("ReceiveData Error: " + ex.Message, "UDP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        #endregion

        #region Other Methods

        private void UpdateStatus(string status)
        {
            rtxtStatus.Text += status + Environment.NewLine;
        }

        #endregion
    }
}