﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Microsoft.Win32;
using System.Runtime.CompilerServices;

namespace MIPSSim
{
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		BindingList<Register> Registers = new BindingList<Register>();
		string[] RegName = { "$zero", "$at", "$v0", "$v1", "$a0", "$a1", "$a2", "$a3", "$t0", "$t1", "$t2", "$t3", "$t4", "$t5", "$t6", "$t7"
							, "$s0", "$s1", "$s2", "$s3", "$s4", "$s5", "$s6", "$s7", "$t8", "$t9", "$k0", "$k1", "$gp", "$sp", "$fp", "$ra"};

		UInt32[] Reg = new UInt32[32];
		UInt32[] TReg = new UInt32[32];
		UInt32[] Pipe = new UInt32[5];
		UInt32 PC;
		Dictionary<UInt32, UInt32> Mem = new Dictionary<uint, uint>();
		Dictionary<UInt32, UInt32> TMem;

		public MainWindow()
		{
			int i;
			InitializeComponent();

			LstReg.ItemsSource = Registers;

			for(i = 0; i < 32; i++)
			{
				Registers.Add(new Register(i, RegName[i]));
			}

			Reset();
		}

		UInt32 MemRead(UInt32 Addr)
		{
			if (!Mem.ContainsKey(Addr))
			{
				return 0;
			}
			return Mem[Addr];
		}

		void MemWrite(UInt32 Addr, UInt32 Data)
		{
			Mem[Addr] = Data;
		}


		void UpdateScreen()
		{
			int i;
			for (i = 0; i < 32; i++)
			{
				Registers[i].Val = Reg[i].ToString("X8");
			}

			TxtPc.Text = PC.ToString("X8");

			TxtInst.Text = IDecode(Pipe[0]);

			TxtIF.Text = IDecode(Pipe[0]);
			TxtID.Text = IDecode(Pipe[1]);
			TxtEX.Text = IDecode(Pipe[2]);
			TxtMEM.Text = IDecode(Pipe[3]);
			TxtWB.Text = IDecode(Pipe[4]);

			TxtIFI.Text = Pipe[0].ToString("X8");
			TxtIDI.Text = Pipe[1].ToString("X8");
			TxtEXI.Text = Pipe[2].ToString("X8");
			TxtMEMI.Text = Pipe[3].ToString("X8");
			TxtWBI.Text = Pipe[4].ToString("X8");
		}

		string IDecode(UInt32 i)
		{
			UInt32 op, s, t, d, h, imm, func;
			string inst;
			op = i >> 26;
			s = i >> 21 & 0x1f;
			t = i >> 16 & 0x1f;
			d = i >> 11 & 0x1f;
			h = i >> 6 & 0x1f;
			imm = i & 0xffff;
			func = i & 0x3f;

			switch (op)
			{
				case 0: // R Type
					switch (func)
					{
						case 0x20:  // Add
							inst = "Add " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x21:  // Addu
							inst = "Addu " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x24:  // And
							inst = "And " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x25:  // Or
							inst = "Or " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x22:  // Sub
							inst = "Sub " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x23:  // Subu
							inst = "Subu " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x00:  // Sll
							inst = "Sll " + RegName[d] + ", " + RegName[s] + ", " + h.ToString("X");
							if (d == 0)
							{
								inst = "Nop";
							}
							break;
						case 0x02:  // Srl
							inst = "Srl " + RegName[d] + ", " + RegName[s] + ", " + h.ToString("X");
							break;
						case 0x2a:  // Slt
							inst = "Slt " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						case 0x2b:  // Sltu
							inst = "Sltu " + RegName[d] + ", " + RegName[s] + ", " + RegName[t];
							break;
						default:
							inst = "Err";
							break;
					}
					break;
				case 0x08:  // Addi
					inst = "Addi " + RegName[t] + ", " + RegName[s] + ", " + ((Int16)imm).ToString("X4");
					break;
				case 0x05:  // Bne
					inst = "Bne " + RegName[s] + ", " + RegName[t] + ", " + ((Int16)imm).ToString("X4");
					break;
				case 0x07:  // Bgtz
					inst = "Bgtz " + RegName[s] + ", " + ((Int16)imm).ToString("X4");
					break;
				case 0x04:  // Beq
					inst = "Beq " + RegName[s] + ", " + RegName[t] + ", " + ((Int16)imm).ToString("X4");
					break;
				case 0x2b:  // Sw
					inst = "Sw " + RegName[t] + ", " + ((Int16)imm).ToString("X4") + "(" + RegName[s] + ")";
					break;
				case 0x23:  // Lw
					inst = "Lw " + RegName[t] + ", " + ((Int16)imm).ToString("X4") + "(" + RegName[s] + ")";
					break;
				default:
					inst = "Err";
					break;
			}

			return inst;
		}

		bool Clk()
		{
			int i;
			
			if (Branch()){
				Run(Pipe[4]);
				for (i = 3; i > 1; i--)
				{
					Pipe[i + 1] = Pipe[i];
				}
				for (i = 1; i < 3; i++)
				{
					Pipe[i] = 0x00000000;
				}
				PC += 4;
				Pipe[0] = MemRead(PC);
			}
			else if (Stall()){
				Run(Pipe[4]);
				for (i = 3; i > 1; i--)
				{
					Pipe[i + 1] = Pipe[i];
				}
				Pipe[2] = 0x00000000;
			}
			else
			{
				Run(Pipe[4]);
				for (i = 3; i > -1; i--)
				{
					Pipe[i + 1] = Pipe[i];
				}
				PC += 4;
				Pipe[0] = MemRead(PC);
			}

			UpdateScreen();

			return true;
		}

		bool Stall()
		{
			UInt32 inst, inst2, op, s, t, t2;

			inst = Pipe[1];
			inst2 = Pipe[2];
			op = inst2 >> 26;
			s = inst >> 21 & 0x1f;
			t = inst >> 16 & 0x1f;
			t2 = inst2 >> 16 & 0x1f;
			if (op == 0x23 && (t2 == t || t2 == s))
			{
				return true;
			}

			return false;
		}

		bool Branch()
		{
			UInt32 inst, op, s, t, d, h, imm, func;
			bool b = false;
			int i;

			inst = Pipe[2];
			op = inst >> 26;

			if (op == 0x05 || op == 0x07 || op == 0x04)
			{

				for (i = 0; i < 32; i++)
				{
					TReg[i] = Reg[i];
				}
				TMem = new Dictionary<uint, uint>(Mem);

				Run(Pipe[4]);
				Run(Pipe[3]);

				op = inst >> 26;
				s = inst >> 21 & 0x1f;
				t = inst >> 16 & 0x1f;
				d = inst >> 11 & 0x1f;
				h = inst >> 6 & 0x1f;
				imm = inst & 0xffff;
				func = inst & 0x3f;

				switch (op)
				{
					case 0x05:  // Bne
						if (Reg[s] != Reg[t])
						{
							PC = (UInt32)((UInt32)PC + (Int16)imm * 4) - 12;
							b = true;
						}
						break;
					case 0x07:  // Bgtz
						if ((Int32)Reg[s] > 0)
						{
							PC = (UInt32)((UInt32)PC + (Int16)imm * 4) - 12;
							b = true;
						}
						break;
					case 0x04:  // Beq
						if (Reg[s] == Reg[t])
						{
							PC = (UInt32)((UInt32)PC + (Int16)imm * 4) - 12;
							b = true;
						}
						break;
					default:
						break;
				}

				for (i = 0; i < 32; i++)
				{
					Reg[i] = TReg[i];
				}
				Mem = new Dictionary<uint, uint>(TMem);
			}

			return b;
		}

		bool Run(UInt32 inst)
		{
			UInt32 op, s, t, d, h, imm, func;
			op = inst >> 26;
			s = inst >> 21 & 0x1f;
			t = inst >> 16 & 0x1f;
			d = inst >> 11 & 0x1f;
			h = inst >> 6 & 0x1f;
			imm = inst & 0xffff;
			func = inst & 0x3f;

			switch (op)
			{
				case 0: // R Type
					switch (func)
					{
						case 0x20:  // Add
							Reg[d] = (UInt32)((Int32)Reg[s] + (Int32)Reg[t]);
							break;
						case 0x21:  // Addu
							Reg[d] = Reg[s] + Reg[t];
							break;
						case 0x24:  // And
							Reg[d] = Reg[s] & Reg[t];
							break;
						case 0x25:  // Or
							Reg[d] = Reg[s] | Reg[t];
							break;
						case 0x22:  // Sub
							Reg[d] = (UInt32)((Int32)Reg[s] - (Int32)Reg[t]);
							break;
						case 0x23:  // Subu
							Reg[d] = Reg[s] - Reg[t];
							break;
						case 0x00:  // Sll
							Reg[d] = Reg[s] << (int)h;
							break;
						case 0x02:  // Srl
							Reg[d] = Reg[s] >> (int)h;
							break;
						case 0x2a:  // Slt
							if ((Int32)Reg[s] < (Int32)Reg[t])
							{
								Reg[d] = 1;
							}
							else
							{
								Reg[d] = 0;
							}
							break;
						case 0x2b:  // Sltu
							if (Reg[s] < Reg[t])
							{
								Reg[d] = 1;
							}
							else
							{
								Reg[d] = 0;
							}
							break;
						default:
							return false;
					}
					break;
				case 0x08:  // Addi
					Reg[t] = (UInt32)((Int32)Reg[s] + (Int16)imm);
					break;
				case 0x2b:  // Sw
					MemWrite((UInt32)((UInt32)Reg[s] + (Int16)imm), Reg[t]);
					break;
				case 0x23:  // Lw
					Reg[t] = MemRead((UInt32)((UInt32)Reg[s] + (Int16)imm));
					break;
				default:
					return false;
			}

			Reg[0] = 0;

			return true;
		}

		private void BtnClk_Click(object sender, RoutedEventArgs e)
		{
			Clk();
		}

		private void BtnMemClr_Click(object sender, RoutedEventArgs e)
		{
			Mem.Clear();
		}

		private void BtnMemLoad_Click(object sender, RoutedEventArgs e)
		{
			int i, j;
			UInt32 a, b;
			string line;
			OpenFileDialog dlg = new OpenFileDialog();
			if (dlg.ShowDialog() == true)
			{
				StreamReader r = new StreamReader(dlg.FileName);
				while (!r.EndOfStream)
				{
					line = r.ReadLine();
					for(i = 0; i < line.Length; i++)
					{
						if (line[i] == '/')
						{
							break;
						}
					}
					for (j = i + 1; j < line.Length; j++)
					{
						if (line[j] == ';')
						{
							break;
						}
					}

					if(j < line.Length)
					{
						a = UInt32.Parse(line.Substring(0, i - 1), System.Globalization.NumberStyles.HexNumber);
						b = UInt32.Parse(line.Substring(i + 1, j - i - 1), System.Globalization.NumberStyles.HexNumber);
						Mem[a] = b;
					}
				}

				UpdateScreen();
			}
		}

		void Reset()
		{
			PC = 0x0040001C;
			int i;
			for (i = 0; i < 32; i++)
			{
				Reg[i] = 0;
			}
			Reg[29] = 0x7fffffff;
			for (i = 0; i < 5; i++)
			{
				Pipe[i] = 0;
			}
			Clk();
			UpdateScreen();
		}

		private void BtnReset_Click(object sender, RoutedEventArgs e)
		{
			Reset();
		}
	}

	public class Register : INotifyPropertyChanged
	{
		string val;

		private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		public Register(int id, string name)
		{
			Name = name;
			Id = "$" + id.ToString();
			Val = "";
		}
		public string Name { get; set; }
		public string Id { get; set; }
		public string Val
		{
			get
			{
				return val;
			}
			set
			{
				val = value;
				NotifyPropertyChanged("Val");
			}
		}
		public event PropertyChangedEventHandler PropertyChanged;
	}

}
