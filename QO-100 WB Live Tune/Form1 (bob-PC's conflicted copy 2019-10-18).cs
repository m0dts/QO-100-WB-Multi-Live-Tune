﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WebSocketSharp;
using WebSocketSharp.Net;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace QO_100_WB_Live_Tune
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            this.FormClosing += Form1_FormClosing;
            Load += new EventHandler(Form1_Load);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Restore things here
            Properties.Settings.Default.Reload();
            foreach (String item in Properties.Settings.Default.ReceiverList)
            {
                string[] vals = item.Split(',');
                RxList.Items.Add(new ListViewItem(vals));
            }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            //Save things here
            System.Collections.Specialized.StringCollection Rxs = new System.Collections.Specialized.StringCollection();
            for (int a = 0; a < RxList.Items.Count; a++)
            {
                Rxs.Add(RxList.Items[a].SubItems[0].Text + "," + RxList.Items[a].SubItems[1].Text + "," + RxList.Items[a].SubItems[2].Text);
            }
            Properties.Settings.Default.ReceiverList = Rxs;
            Properties.Settings.Default.Save();
        }




        private WebSocket ws;       //websocket client

        static int width = 922;     //web monitor uses 922 points, 6 padded?
        static int height = 255;    //makes things easier
        static Bitmap bmp = new Bitmap(width, height);
        Pen greenpen = new Pen(Color.FromArgb(200, 20, 200, 20));
        Pen greypen = new Pen(Color.Gray, width: 1) { DashPattern = new[] { 10f, 10f } };
        SolidBrush shadowBrush = new SolidBrush(Color.FromArgb(75, Color.Gray));
        Graphics tmp = Graphics.FromImage(bmp);

        UInt16[] fft_data;  //array of spectrum values
        List<List<double>> signals = new List<List<double>>();  //list of signals found: 
        bool connected = false;

        //udp port stuff
        UdpClient MT_Client = new UdpClient();
        System.Net.IPEndPoint sending_end_point;


        int[,] rx_blocks= new int[4,3];



        private void button1_Click(object sender, EventArgs e)
        {
            if (!connected) {
                ws = new WebSocket("wss://eshail.batc.org.uk/wb/fft");
                ws.OnMessage += (ss, ee) => NewData(sender, ee.RawData);
                ws.OnOpen += (ss, ee) => { connected = true; button1.Text = "Disconnect"; };
                ws.OnClose += (ss, ee) => { connected = false; button1.Text = "Connect"; };
                ws.Connect();
            }
            else{
                ws.Close();
            }
        }

        private void NewData(object sender, byte[] data)
        {
            //Console.WriteLine(data[0]);
            fft_data = new UInt16[data.Length/2];
            

            //unpack bytes to unsigned short int values
            int n = 0;
            byte[] buf=new byte[2];

            for (int i = 0; i < data.Length; i += 2)
            {
                buf[0] = data[i];
                buf[1] = data[i + 1];
                fft_data[n]= BitConverter.ToUInt16(buf, 0);
                n++;
            }

            //detect signals, stolen from web monitor javascript ;-)
            detect_signals(fft_data);

            //draw the spectrum
            drawspectrum(fft_data,signals);

        }

        private void drawspectrum(UInt16[] fft_data, List<List<double>> signals)
        {
            int receivers = RxList.Items.Count;
            tmp.Clear(Color.Black);     //clear canvas


            //draw lines to segment y axis determining where to click for each receiver
            if (receivers >= 1)
            {
                int y = 0;
                int tyoffset = 0;
                for (int i = 0; i < receivers; i++)
                {
                    y = 255 - ((255 / receivers) * i + 2);
                    tyoffset= (255 / receivers) / 2+10;
                    if (i >0)
                    {
                        tmp.DrawLine(greypen, 0, y, 922, y);
                    }
                    tmp.DrawString((i+1).ToString(), new Font("Tahoma", 10), Brushes.White, new PointF(Convert.ToSingle(0), (255 - tyoffset-Convert.ToSingle((255 / receivers) * i + 1))));
                    if(rx_blocks[i, 0] >0)
                    {
                        //draw block showing signal selected
                        
                        tmp.FillRectangles(shadowBrush, new RectangleF[] { new Rectangle(rx_blocks[i, 0] - (rx_blocks[i, 1] / 2), 255-y, rx_blocks[i, 1], (255 / receivers)) });

                    }
                }
            }


            for (int i = 1; i < fft_data.Length-6; i++)
            {
                tmp.DrawLine(greenpen, i-1, 255-fft_data[i-1]/255, i, 255-fft_data[i]/255);
            }
            foreach (List<double> sig in signals)
            {
                tmp.DrawString(sig[1].ToString("#.000") +" "+ sig[2].ToString("#.000Ms"),new Font("Tahoma", 10),Brushes.White,new PointF(Convert.ToSingle(sig[0]-50), (255-Convert.ToSingle(sig[3]+30))));
            }
            spectrum.Image = bmp;
        }

        private void spectrum_Click(object sender, EventArgs e)
        {
            MouseEventArgs me = (MouseEventArgs)e;
            Point pos = me.Location;
            foreach (List<double> sig in signals)
            {
                if (pos.X > sig[4] & pos.X < sig[5])
                {
                    
                    int rx = 1;

                    switch (RxList.Items.Count)
                    {
                        case 1:
                            //use full v scale for selecting which Rx
                            rx = 1;
                            break;
                        case 2:
                            //use halves of v scale for selecting which Rx
                            if (pos.Y > 127)
                            {
                                //Rx2
                                rx = 2;
                            }
                            else
                            {
                                //Rx1
                                rx = 1;
                            }
                            break;

                        case 3:
                            //use thirds of v scale for selecting which Rx
                            if (pos.Y > 170)
                            {
                                //Rx3
                                rx = 3;
                            }
                            else
                            {
                                if (pos.Y > 85)
                                {
                                    //Rx2
                                    rx = 2;
                                }
                                else
                                {
                                    //Rx1
                                    rx = 1;
                                }

                            }
                            break;

                        case 4:
                            //use quarters of v scale for selecting which Rx
                            if (pos.Y > 192)
                            {
                                //Rx4
                                rx = 4;
                            }
                            else
                            {
                                if (pos.Y > 128)
                                {
                                    //Rx3
                                    rx = 3;
                                }
                                else
                                {
                                    if (pos.Y > 64)
                                    {
                                        //Rx2
                                        rx = 2;
                                    }
                                    else
                                    {
                                        //Rx1
                                        rx = 1;
                                    }
                                }

                            }
                            break;

                    }

                    rx_blocks[rx-1,0] = Convert.ToInt16(sig[0]);
                    rx_blocks[rx-1, 1] = Convert.ToInt16(sig[5] - sig[4]);
                    rx_blocks[rx-1, 2] = rx;
                    int freq = Convert.ToInt32((sig[1] + 10000.0) * 1000);
                    int sr = Convert.ToInt32((sig[2] * 1000.0));
                    //send minitioune udp string
                    byte[] outStream = Encoding.ASCII.GetBytes("[GlobalMsg],Freq=" + freq.ToString() + ",Offset=" + RxList.Items[rx-1].SubItems[2].Text + ",Doppler=0,Srate=" + sr.ToString() + ",WideScan=0,LowSR=0,DVBmode=Auto,FPlug=A,Voltage=0,22kHz=off\n");

                    IPAddress ip = System.Net.IPAddress.Parse(RxList.Items[rx - 1].SubItems[0].Text);
                    int port = Convert.ToInt16(RxList.Items[rx - 1].SubItems[1].Text);
                    sending_end_point = new System.Net.IPEndPoint(ip,port);
                    MT_Client.Client.SendTo(outStream, sending_end_point);

                }
            }
        }

        public float align_symbolrate(float width)
        {
            //console.log(width);
            if (width < 0.022)
            {
                return 0;
            }
            else if (width < 0.060)
            {
                return 0.035f;
            }
            else if (width < 0.086)
            {
                return 0.066f;
            }
            else if (width < 0.185)
            {
                return 0.125f;
            }
            else if (width < 0.277)
            {
                return 0.250f;
            }
            else if (width < 0.388)
            {
                return 0.333f;
            }
            else if (width < 0.700)
            {
                return 0.500f;
            }
            else if (width < 1.2)
            {
                return 1.000f;
            }
            else if (width < 1.6)
            {
                return 1.500f;
            }
            else if (width < 2.2)
            {
                return 2.000f;
            }
            else
            {
                return Convert.ToSingle(Math.Round(width * 5) / 5.0);
            }
        }



        public void detect_signals(UInt16[] fft_data)
        {

            signals.Clear();
            int i;
            int j;

            int noise_level = 11000;
            int signal_threshold = 16000;

            Boolean in_signal = false;
            int start_signal=0;
            int end_signal;
            float mid_signal;
            int strength_signal;
            float signal_bw;
            float signal_freq;
            int acc;
            int acc_i;




            for (i = 2; i < fft_data.Length; i++)
            {
                if (!in_signal)
                {
                    if ((fft_data[i] + fft_data[i - 1] + fft_data[i - 2]) / 3.0 > signal_threshold)
                    {
                        in_signal = true;
                        start_signal = i;
                    }
                }
                else /* in_signal == true */
                {
                    if ((fft_data[i] + fft_data[i - 1] + fft_data[i - 2]) / 3.0 < signal_threshold)
                    {
                        in_signal = false;

                        end_signal = i;
                        acc = 0;
                        acc_i = 0;
                        for (j = Convert.ToInt16(start_signal + (0.3 * (end_signal - start_signal))) | 0; j < start_signal + (0.7 * (end_signal - start_signal)); j++)
                        {
                            acc = acc + fft_data[j];
                            acc_i = acc_i + 1;
                        }
                   

                        strength_signal = acc / acc_i;
            
                        /* Find real start of top of signal */
                        for (j = start_signal; (fft_data[j] - noise_level) < 0.75 * (strength_signal - noise_level); j++)
                        {
                            start_signal = j;
                        }
      

                        /* Find real end of the top of signal */
                        for (j = end_signal; (fft_data[j] - noise_level) < 0.75 * (strength_signal - noise_level); j--)
                        {
                            end_signal = j;
                        }
               
                        mid_signal = Convert.ToSingle(start_signal + ((end_signal - start_signal) / 2.0));

                        signal_bw = align_symbolrate(Convert.ToSingle((end_signal - start_signal) * (9.0 / fft_data.Length)));
                        signal_freq = Convert.ToSingle(490.75 + (((mid_signal + 1) / fft_data.Length) * 9.0));

                        // Exclude signals in beacon band
                       if (signal_bw >= 0.033)
                       {
                            signals.Add(new List<double> { mid_signal, signal_freq, signal_bw,strength_signal/255,start_signal,end_signal });

                       }


                    }
                }
            }
         
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (RxList.Items.Count < 4)
            {
                IPAddress address;
                int port;
                int offset;
                if (IPAddress.TryParse(mt_ip.Text, out address) & int.TryParse(mt_port.Text, out port) & int.TryParse(lnb_offset.Text, out offset))
                {
                    RxList.Items.Add(new ListViewItem(new String[] { address.ToString(), port.ToString(), offset.ToString() }));
                }
                else
                {
                    MessageBox.Show("Invalid IP/Port/LO entered!");
                }
            }
            else
            {
                MessageBox.Show("Up to four receivers only");
            }
        }

        private void RxList_DoubleClick(object sender, EventArgs e)
        {
            if (RxList.SelectedIndices.Count > 0)
            {
                RxList.Items[Convert.ToInt16(RxList.SelectedIndices[0])].Remove();
            }
        }

    
        private void RxList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}