﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.ComponentModel;

namespace PeekPoker.RealTimeMemory
{
    /// <summary>Real Time Memory Access Class using xbdm
    /// NB: Large dump speed depends on the version of xbdm you have</summary>
    public class RealTimeMemory
    {
        #region Eventhandlers/DelegateHandlers
        public event UpdateProgressBarHandler ReportProgress;
        #endregion

        private readonly string _ipAddress;
        private bool _connected;
        private bool _memexValidConnection;
        private uint _startDumpOffset;
        private uint _startDumpLength;
        private bool _stopSearch;
        private TcpClient _tcp;
        private RWStream _readWriter;

        #region Constructor
        /// <summary>RealTimeMemory constructor Example: Default start dump = 0xC0000000 and length = 0x1FFFFFFF</summary>
        /// <param name="ipAddress">The IP address</param>
        /// <param name="startDumpOffset">The start dump address</param>
        /// <param name="startDumpLength">The dump length</param>
        public RealTimeMemory(string ipAddress, uint startDumpOffset, uint startDumpLength)
        {
            _ipAddress = ipAddress;
            _connected = false;
            //fullmemory dump by default
            _startDumpOffset = startDumpOffset;
            _startDumpLength = startDumpLength;
        }
        #endregion
        
        #region Methods
        /// <summary>Connect to the  using port 730 using the given ip address</summary>
        /// <returns>True if connection was successful and False if not</returns>
        public bool Connect()
        {
            try
            {
                if (_ipAddress.Length < 5)
                    throw new Exception("Invalid IP");
                if (_connected) return true; //If you are already connected then return
                _tcp = new TcpClient(); //New Istance of TCP
                //Connect to the specified host using port 730
                _tcp.Connect(_ipAddress, 730);
                var response = new byte[1024];
                _tcp.Client.Receive(response);
                string reponseString = Encoding.ASCII.GetString(response).Replace("\0", "");
                //validate connection
                _connected = reponseString.Substring(0, 3) == "201";

                return _connected;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        /// <summary>Poke the Memory</summary>
        /// <param name="memoryAddress">The memory addess to Poke Example:0xCEADEADE - Uses *.FindOffset</param>
        /// <param name="value">The value to poke Example:000032FF (hex string)</param>
        public void Poke(string memoryAddress, string value)
        {
            Poke(Convert(memoryAddress), value);
        }
       
        /// <summary>Poke the Memory</summary>
        /// <param name="memoryAddress">The memory addess to Poke Example:0xCEADEADE - Uses *.FindOffset</param>
        /// <param name="value">The value to poke Example:000032FF (hex string)</param>
        private void Poke(uint memoryAddress, string value)
        {
            if (!Functions.IsHex(value))
                throw new Exception("Not a valid Hex String!");
            if (!Connect()) return; //Call function - If not connected return
            try
            {
                if (memoryAddress > (_startDumpOffset + _startDumpLength) || memoryAddress < _startDumpOffset)
                    throw new Exception("Memory Address Out of Bounds");
                WriteMemory(memoryAddress, value); //Items 1 flame grenade
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                _tcp.Close(); //close connection
                _connected = false;
            }
        }

        /// <summary>Peek into the Memory</summary>
        /// <param name="startDumpAddress">The Hex offset to start dump Example:0xC0000000 </param>
        /// <param name="dumpLength">The Length or size of dump Example:0xFFFFFF </param>
        /// <param name="memoryAddress">The memory address to peek Example:0xC5352525 </param>
        /// <param name="peekSize">The byte size to peek Example: "0x4" or "4"</param>
        /// <returns>Return the hex string of the value</returns>
        public string Peek(string startDumpAddress, string dumpLength, string memoryAddress, string peekSize)
        {
            return Peek(Convert(startDumpAddress), Convert(dumpLength), Convert(memoryAddress), ConvertSigned(peekSize));
        }
        /// <summary>Peek into the Memory</summary>
        /// <param name="startDumpAddress">The Hex offset to start dump Example:0xC0000000 </param>
        /// <param name="dumpLength">The Length or size of dump Example:0xFFFFFF </param>
        /// <param name="memoryAddress">The memory address to peek Example:0xC5352525 </param>
        /// <param name="peekSize">The byte size to peek Example: "0x4" or "4"</param>
        /// <returns>Return the hex string of the value</returns>
        private string Peek(uint startDumpAddress, uint dumpLength, uint memoryAddress, int peekSize)
        {
            var total = (memoryAddress - startDumpAddress);
            if (memoryAddress > (startDumpAddress + dumpLength) || memoryAddress < startDumpAddress)
                throw new Exception("Memory Address Out of Bounds");

            if (!Connect()) return null; //Call function - If not connected return
            if (!GetMeMex(startDumpAddress, dumpLength)) return null; //call function - If not connected or if somethign wrong return

            try
            {

                    var readWriter = new RWStream();
                    var data = new byte[1026]; //byte chuncks

                    //Writing each byte chuncks========
                    //No need to mess with it :D
                    for (var i = 0; i < dumpLength / 1024; i++)
                    {
                        _tcp.Client.Receive(data);
                        readWriter.WriteBytes(data, 2, 1024);
                    }
                    //Write whatever is left
                    var extra = (int)(dumpLength % 1024);
                    if (extra > 0)
                    {
                        _tcp.Client.Receive(data);
                        readWriter.WriteBytes(data, 2, extra);
                    }
                    readWriter.Flush();
                    //===================================
                    //===================================
                    readWriter.Position = total;
                    var value = readWriter.ReadBytes(peekSize);
                    readWriter.Close();
                    return Functions.ToHexString(value);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                _tcp.Close(); //close connection
                _connected = false;
                _memexValidConnection = false;
            }
        }

        /// <summary>Find the address of a pointer from the start dump offset</summary>
        /// <param name="pointer">The hex string of the pointer Example: 821122114455EEFF000000</param>
        /// <returns>Returns and array of the address or all address where the pointer was found</returns>
        public List<string> FindHexOffset(string pointer)
        {
            if (!Functions.IsHex(pointer))
                throw new Exception(string.Format("{0} is not a valid Hex string.", pointer));
            if (!Connect()) return null; //Call function - If not connected return
            if (!GetMeMex()) return null; //call function - If not connected or if somethign wrong return

            try
            {
                    //LENGTH or Szie = Length of the dump
                    var size = _startDumpLength;
                    _readWriter = new RWStream();
                    _readWriter.ReportProgress += ReportProgress;
                    var data = new byte[1026]; //byte chuncks

                    //Writing each byte chuncks========
                    //No need to mess with it :D
                    for (var i = 0; i < size / 1024; i++)
                    {
                        _tcp.Client.Receive(data);
                        _readWriter.WriteBytes(data, 2, 1024);
                        ReportProgress(0, (int)(size / 1024), (i + 1), "Reading Dump...");
                    }
                    //Write whatever is left
                    var extra = (int)(size % 1024);
                    if (extra > 0)
                    {
                        _tcp.Client.Receive(data);
                        _readWriter.WriteBytes(data, 2, extra);
                    }
                    _readWriter.Flush();
                    //===================================
                    //===================================
                    _readWriter.Position = 0;
                    var values = _readWriter.SearchHexString(pointer, false);
                    var addresses = new List<string>(values.Length);
                    int x = 0;
                    foreach (var value in values)
                    {
                        addresses.Add(Functions.ToHexString(Functions.UInt32ToBytes(_startDumpOffset + (uint)value)));
                        ReportProgress(0, values.Length, x, "Add Results to list");
                        x++;
                    }
                    _readWriter.Close();
                    return new List<string>(addresses.ToArray());
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                _tcp.Close(); //close connection
                _connected = false;
                _memexValidConnection = false;
            }
        }
        // Experimental
        public BindingList<Types.SearchResults> ExFindHexOffset(string pointer)
        {
            if (!Functions.IsHex(pointer))
                throw new Exception(string.Format("{0} is not a valid Hex string.", pointer));
            if (!Connect()) return null; //Call function - If not connected return
            if (!GetMeMex()) return null; //call function - If not connected or if somethign wrong return

            try
            {
                //LENGTH or Szie = Length of the dump
                var size = _startDumpLength;
                _readWriter = new RWStream();
                _readWriter.ReportProgress += new UpdateProgressBarHandler(ReportProgress);
                var data = new byte[1026]; //byte chuncks

                //Writing each byte chuncks========
                //No need to mess with it :D
                for (var i = 0; i < size / 1024; i++)
                {
                    _tcp.Client.Receive(data);
                    _readWriter.WriteBytes(data, 2, 1024);
                    ReportProgress(0, (int)(size / 1024), (i + 1), "Reading Dump...");
                }
                //Write whatever is left
                var extra = (int)(size % 1024);
                if (extra > 0)
                {
                    _tcp.Client.Receive(data);
                    _readWriter.WriteBytes(data, 2, extra);
                }
                _readWriter.Flush();
                //===================================
                //===================================
                _readWriter.Position = 0;

                //using the Experimental search Function
                //List<Types.SearchResults> values = readWriter.ExSearchHexString(pointer, _startDumpOffset, false);
                var values = _readWriter.Ex2SearchHexString(Functions.StringToByteArray(pointer), _startDumpOffset);

                _readWriter.Close();
                return values;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
            finally
            {
                _tcp.Close(); //close connection
                _connected = false;
                _memexValidConnection = false;
            }
        }

        #region Private
        private void WriteMemory(uint address, string data)
        {
            // Send the setmem command
            _tcp.Client.Send(Encoding.ASCII.GetBytes(string.Format("SETMEM ADDR=0x{0} DATA={1}\r\n", address.ToString("X2"), data)));

            // Check to see our response
            var packet = new byte[1026];
            _tcp.Client.Receive(packet);
        }
        private bool GetMeMex()
        {
            return GetMeMex(_startDumpOffset, _startDumpLength);
        }
        private bool GetMeMex(uint startDump, uint length)
        {
            if (_memexValidConnection) return true;
            //ADDR=0xDA1D0000 - The start offset in the physical memory I want the dump to start
            //LENGTH = Length of the dump
            _tcp.Client.Send(Encoding.ASCII.GetBytes(string.Format("GETMEMEX ADDR={0} LENGTH={1}\r\n", startDump, length)));
            var response = new byte[1024];
            _tcp.Client.Receive(response);
            var reponseString = Encoding.ASCII.GetString(response).Replace("\0", "");
            //validate connection
            _memexValidConnection = reponseString.Substring(0, 3) == "203";
            return _memexValidConnection;
        }
        private uint Convert(string value)
        {
            if (value.Contains("0x"))
                return System.Convert.ToUInt32(value.Substring(2), 16);
            return System.Convert.ToUInt32(value);
        }

        private int ConvertSigned(string value)
        {
            if (value.Contains("0x"))
                return System.Convert.ToInt32(value.Substring(2), 16);
            return System.Convert.ToInt32(value);
        }

        #endregion
        #endregion

        #region Properties
        /// <summary>Set or Get the start dump offset</summary>
        public uint DumpOffset
        {
            set { _startDumpOffset = value; }
        }
        /// <summary>Set or Get the dump length</summary>
        public uint DumpLength
        {
            set { _startDumpLength = value; }
        }
        public bool StopSearch
        {
            get
            {
                if (!_readWriter.Accessed)
                    return false;
                return _readWriter.StopSearch;
            }
            set
            {
                if (!_readWriter.Accessed)
                    return;
                    _readWriter.StopSearch = value;
            }
        }

        #endregion
    }
}