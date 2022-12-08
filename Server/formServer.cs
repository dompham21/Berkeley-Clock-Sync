using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using static System.Net.Mime.MediaTypeNames;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Server
{
    public partial class Server : Form
    {
        TcpListener listener;
        TcpClient client;
        String clNo;
        Dictionary<string, TcpClient> clientList = new Dictionary<string, TcpClient>();
        CancellationTokenSource cancellation;
        List<string> chat = new List<string>();
        Dictionary<string, DateTime> clientClock = new Dictionary<string, DateTime>();
        int clientNumber = 1;
        DateTime currentTime;

        public Server()
        {
            CheckForIllegalCrossThreadCalls = false;
            InitializeComponent();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            cancellation = new CancellationTokenSource(); //resets the token when the server restarts
            await startServer();
            Debug.WriteLine("cross start server");
        }

        public void updateUI(String m)
        {
            textBox1.AppendText(">>" + m + Environment.NewLine);
        }

        public async Task startServer()
        {
            listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 5000);
            listener.Start();
            updateUI("Server Started at " + listener.LocalEndpoint);
            updateUI("Waiting for Clients");
            try
            {
                int counter = 0;
                while (true)
                {
                    counter++;
                    client = await listener.AcceptTcpClientAsync(); // accept incoming request ... 


                    /* get username */
                    byte[] name = new byte[50];
                    NetworkStream stre = client.GetStream(); //Gets The Stream of The Connection
                    stre.Read(name, 0, name.Length); //Receives Data 
                    String username = Encoding.ASCII.GetString(name); // Converts Bytes Received to String
                    username = username.Substring(0, username.IndexOf("$"));

                    /* add to dictionary, listbox and send userList  */
                    clientList.Add(username, client);
                    listBox1.Items.Add(username);
                    updateUI("Connected to user " + username + " - " + client.Client.RemoteEndPoint);
                    announce(username + " Joined ", username, false); // sent msg againt client for join notify

                    await Task.Delay(1000).ContinueWith(t => sendUsersList()); // sent list of Client Online to each 


                    Task receiveTask = new Task(() =>
                    {
                        ServerReceive(client, username);
                    });
                    receiveTask.Start();
                }
            }
            catch (Exception)
            {
                listener.Stop();
            }

        }
        /// <summary>
        /// When server received msg
        /// Then annouce to each Client on connecting with server if they are in Global msg
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="uName"></param>
        /// <param name="flag"></param>
        public void announce(string msg, string uName, bool flag)
        {
            try
            {
                foreach (var Item in clientList) // loop each client
                {
                    TcpClient broadcastSocket;
                    broadcastSocket = (TcpClient)Item.Value;
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    Byte[] broadcastBytes = null;

                    if (flag)
                    {
                        //broadcastBytes = Encoding.ASCII.GetBytes("gChat|*|" + uName + " says : " + msg);

                        chat.Add("gChat");
                        chat.Add(uName + " says : " + msg);
                        broadcastBytes = ObjectToByteArray(chat);
                    }
                    else
                    {
                        //broadcastBytes = Encoding.ASCII.GetBytes("gChat|*|" + msg);

                        chat.Add("gChat");
                        chat.Add(msg);
                        broadcastBytes = ObjectToByteArray(chat);

                    }

                    broadcastStream.Write(broadcastBytes, 0, broadcastBytes.Length); // sen to client msg
                    broadcastStream.Flush();
                    chat.Clear();
                }
            }
            catch (Exception)
            {

            }
        }  //end broadcast function


        public Object ByteArrayToObject(byte[] arrBytes)
        {
            using (var memStream = new MemoryStream())
            {
                var binForm = new BinaryFormatter();
                memStream.Write(arrBytes, 0, arrBytes.Length);
                memStream.Seek(0, SeekOrigin.Begin);
                var obj = binForm.Deserialize(memStream);
                return obj;
            }
        }

        public byte[] ObjectToByteArray(Object obj)
        {
            BinaryFormatter bf = new BinaryFormatter();
            using (var ms = new MemoryStream())
            {
                bf.Serialize(ms, obj);
                return ms.ToArray();
            }
        }


        /// <summary>
        /// This chat system design that client will be sent a msg as List<string> with list[0] as Key and list[1] as Content
        /// Key is signature of Global chat or Private chat
        /// Content is what client want to sent
        /// </summary>
        /// <param name="clientn"></param>
        /// <param name="username"></param>
        public void ServerReceive(TcpClient clientn, String username)
        {
            byte[] data = new byte[1024]; // buffer
            String text = null;
            while (true)
            {
                try
                {
                    NetworkStream stream = clientn.GetStream(); //Gets The Stream of The Connection
                    stream.Read(data, 0, data.Length); //Receives Data 
                    List<string> parts = (List<string>)ByteArrayToObject(data);


                    switch (parts[0])
                    {
                        case "gChat":
                            this.Invoke((MethodInvoker)delegate // To Write the Received data
                            {
                                textBox1.Text += username + ": " + parts[1] + Environment.NewLine;
                            });
                            announce(parts[1], username, true);
                            break;
                        case "sync":
                            this.Invoke((MethodInvoker)delegate // To Write the Received data
                            {
                                textBox1.Text += username + " time: " + parts[1] + Environment.NewLine;
                            });
                            clientClock.Add(username, DateTime.ParseExact(parts[1], "HH:mm:ss", CultureInfo.InvariantCulture));
                            if(clientClock.Count == clientNumber)
                            {
                                

                                foreach (KeyValuePair<string, DateTime> kvp in clientClock)
                                {
                                    textBox1.Text += "Time Difference: " + kvp.Key + Environment.NewLine;
                                    TimeSpan span = currentTime.Subtract(kvp.Value);
                                    textBox1.Text += "Time Difference (seconds): " + span.Seconds + Environment.NewLine;
                                    textBox1.Text += "Time Difference (minutes): " + span.Minutes + Environment.NewLine;
                                    textBox1.Text += "Time Difference (hours): " + span.Hours + Environment.NewLine;
                                    textBox1.Text += "Time Difference: " + span.ToString() + Environment.NewLine;
           
                                }
                            }
                            announce(parts[1], username, true);
                            break;
                    }

                    parts.Clear();


                }
                catch (Exception r)
                {
                    updateUI("Client Disconnected: " + username);
                    announce("Client Disconnected: " + username + "$", username, false);
                    clientList.Remove(username);

                    this.Invoke((MethodInvoker)delegate
                    {
                        listBox1.Items.Remove(username);
                    });
                    sendUsersList();
                    break;
                }
            }
        }

        private void btnSyncTime_Click(object sender, EventArgs e)
        {
            try
            {
                currentTime = DateTime.Now;
                textBox1.Text += "Server time: " + currentTime.ToString("HH:mm:ss") + Environment.NewLine;

                List<string> chat = new List<string>();
                chat.Add("sync");
                byte[] byData = ObjectToByteArray(chat);

                foreach (var Item in clientList) // and sent it to each Client
                {
                    TcpClient broadcastSocket;
                    broadcastSocket = (TcpClient)Item.Value;
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    broadcastStream.Write(byData, 0, byData.Length);
                    broadcastStream.Flush();
                }

                

            }
            catch (SocketException se)
            {
            }

        }

        private void disconnectToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                TcpClient workerSocket = null;

                String clientName = listBox1.GetItemText(listBox1.SelectedItem);
                workerSocket = (TcpClient)clientList.FirstOrDefault(x => x.Key == clientName).Value; //find the client by username in dictionary
                workerSocket.Close();

            }
            catch (SocketException se)
            {
            }
        }
       
        /// <summary>
        /// Get array of ListBox  of all Client Online
        /// sent to each Client
        /// </summary>
        public void sendUsersList()
        {
            try
            {
                byte[] userList = new byte[1024];
                string[] clist = listBox1.Items.OfType<string>().ToArray(); // get array of listbox CLient Online
                List<string> users = new List<string>();

                users.Add("userList");
                foreach (String name in clist)
                {
                    users.Add(name);
                }
                userList = ObjectToByteArray(users); // userList now is byte[] of list Client online 

                foreach (var Item in clientList) // and sent it to each Client
                {
                    TcpClient broadcastSocket;
                    broadcastSocket = (TcpClient)Item.Value;
                    NetworkStream broadcastStream = broadcastSocket.GetStream();
                    broadcastStream.Write(userList, 0, userList.Length);
                    broadcastStream.Flush();
                    users.Clear();
                }
            }
            catch (SocketException se)
            {
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            textBox1.SelectionStart = textBox1.TextLength;
            textBox1.ScrollToCaret();
        }

        private void Server_Load(object sender, EventArgs e)
        {
            timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            time.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
