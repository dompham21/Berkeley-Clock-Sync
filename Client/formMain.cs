﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace Client
{
    public partial class formMain : Form
    {
        public TcpClient clientSocket;
        public NetworkStream serverStream = default(NetworkStream);
        string readData = null;
        Thread ctThread;
        String name = null;
        //Dictionary<string, Object> nowChatting = new Dictionary<string, Object>();
        List<string> nowChatting = new List<string>();
        List<string> chat = new List<string>();

        public void setName(String title)
        {
            this.Text = title;
            name = title;
        }

        public formMain()
        {
            InitializeComponent();

        }

        private void btnConnect_Click(object sender, EventArgs e)
        {
            clientSocket = new TcpClient();
            try
            {
                clientSocket.Connect("127.0.0.1", 5000);
                readData = "Connected to Server ";
                msg();

                serverStream = clientSocket.GetStream();

                byte[] outStream = Encoding.ASCII.GetBytes(name + "$");
                serverStream.Write(outStream, 0, outStream.Length);
                serverStream.Flush();
                btnConnect.Enabled = false;


                ctThread = new Thread(getMessage);
                ctThread.Start();
            }
            catch (Exception)
            {
                MessageBox.Show("Server Not Started");
            }
        }

        public void getUsers(List<string> parts)
        {
            this.Invoke((MethodInvoker)delegate
            {
                listBox1.Items.Clear();
                for (int i = 1; i < parts.Count; i++)
                {
                    listBox1.Items.Add(parts[i]);

                }
            });
        }

        private void getMessage()
        {
            try
            {
                while (true)
                {
                    serverStream = clientSocket.GetStream();
                    byte[] inStream = new byte[10025];
                    serverStream.Read(inStream, 0, inStream.Length);
                    List<string> parts = null;

                    if (!SocketConnected(clientSocket))
                    {
                        MessageBox.Show("You've been Disconnected");
                        ctThread.Abort();
                        clientSocket.Close();
                        btnConnect.Enabled = true;
                    }

                    parts = (List<string>)ByteArrayToObject(inStream);
                    switch (parts[0])
                    {
                        case "userList":
                            getUsers(parts);
                            break;

                        case "gChat":
                            readData = "" + parts[1];
                            msg();
                            break;
                        case "sync":
                            readData = "" + parts[0];
                            msg();
                            chat.Add("sync");
                            chat.Add(DateTime.Now.ToString("HH:mm:ss"));
                            byte[] outStream = ObjectToByteArray(chat);

                            serverStream.Write(outStream, 0, outStream.Length);
                            serverStream.Flush();
                            chat.Clear();
                            break;
                    }

                    if (readData[0].Equals('\0'))
                    {
                        readData = "Reconnect Again";
                        msg();

                        this.Invoke((MethodInvoker)delegate // To Write the Received data
                        {
                            btnConnect.Enabled = true;
                        });

                        ctThread.Abort();
                        clientSocket.Close();
                        break;
                    }
                    chat.Clear();
                }
            }
            catch (Exception e)
            {
                ctThread.Abort();
                clientSocket.Close();
                btnConnect.Enabled = true;
                Console.WriteLine(e);
            }

        }

        private void msg()
        {
            if (this.InvokeRequired)
                this.Invoke(new MethodInvoker(msg));
            else
                history.Text = history.Text + Environment.NewLine + " >> " + readData;
        }

        private void formMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            DialogResult dialog = MessageBox.Show("Do you want to exit? ", "Exit", MessageBoxButtons.YesNo);
            if (dialog == DialogResult.Yes)
            {
                try
                {
                    ctThread.Abort();
                    clientSocket.Close();
                }
                catch (Exception) { }

                Application.ExitThread();
            }
            else if (dialog == DialogResult.No)
            {
                e.Cancel = true;
            }
        }



        public byte[] ObjectToByteArray(object _Object)
        {
            using (var stream = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(stream, _Object);
                return stream.ToArray();
            }
        }

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

        bool SocketConnected(TcpClient s) //check whether client is connected server
        {
            bool flag = false;
            try
            {
                bool part1 = s.Client.Poll(10, SelectMode.SelectRead);
                bool part2 = (s.Available == 0);
                if (part1 && part2)
                {
                    indicator.BackColor = Color.Red;
                    this.Invoke((MethodInvoker)delegate // cross threads
                    {
                        btnConnect.Enabled = true;
                    });
                    flag = false;
                }
                else
                {
                    indicator.BackColor = Color.Green;
                    flag = true;
                }
            }
            catch (Exception er)
            {
                Console.WriteLine(er);
            }
            return flag;
        }

        private void history_TextChanged(object sender, EventArgs e)
        {
            history.SelectionStart = history.TextLength;
            history.ScrollToCaret();
        }


        private void formMain_Load(object sender, EventArgs e)
        {
            timer.Start();
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            time.Text = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
