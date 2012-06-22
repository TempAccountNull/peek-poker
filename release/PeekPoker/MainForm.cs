﻿using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Collections.Generic;
using System.ComponentModel;

//=====================================================
// Namespaces        -> PascalCased                  //
// Class names       -> PascalCased                  //
// Private Variables -> _carmelCased + Underscore    //
// Public Variables  -> PascalCased                  //
// Local Variables   -> carmelCased                  //
// Function Names    -> PascalCased                  //
//=====================================================

namespace PeekPoker
{
    #region Delegates
    public delegate void UpdateProgressBarHandler(int min, int max, int value, string text);
    #endregion

    public partial class MainForm : Form
    {
        #region global varibales

        private RealTimeMemory.RealTimeMemory _rtm;//DLL is now in the Important File Folder
        private readonly AutoCompleteStringCollection _data = new AutoCompleteStringCollection();
        private readonly string _filepath = (Application.StartupPath + "\\XboxIP.txt"); //For IP address loading - 8Ball
        private readonly string _trainerdottext = (Application.StartupPath + "\\Trainers.txt"); //Stores trainer codes in friendly format
        private uint _searchRangeDumpLength;
        private string _dumpFilePath;
        private BindingList<Types.SearchResults> _searchResult = new BindingList<Types.SearchResults>();

        #endregion

        public MainForm()
        {
            InitializeComponent();
            SetLogText("Welcome to Peek Poker.");
            SetLogText("Please make sure you have the xbdm xbox 360 plugin.");
            SetLogText("All the information provided on this application are for educational purposes only. The application or host is no way responsible for any misuse of the information.");
            //Set comboboxes correctly - need to be here for some reason... or it won't work...
            searchRangeBaseValueTypeCB.SelectedIndex = 0;
            searchRangeEndTypeCB.SelectedIndex = 0;
            resultGrid.DataSource = _searchResult;
            combocodetype.SelectedIndex = 2;
        }
        private void MainFormFormClosing(object sender, FormClosingEventArgs e)
        {
            Process.GetCurrentProcess().Kill();//Immidiable stop the process
        }
        private void Form1Load(Object sender, EventArgs e)
        {
            //feature suggested by fairchild
            var xboxname = (string)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\XenonSDK", "XboxName", "NotFound");
            if (xboxname != "NotFound")
                ipAddressTextBox.Text = xboxname;
            //This is for handling automatic loading of the IP address and txt file creation. -8Ball
            //Changed a bit to only check if it does exist creation and fill code is in the same place now - sam
            if (File.Exists(_filepath)) ipAddressTextBox.Text = File.ReadAllText(_filepath);
            
            //Set correct max. min values for the numeric fields
            ChangeNumericMaxMin();

            if (File.Exists(_trainerdottext)) Injectcodes(); //loads trainers.txt
        }
        private void AboutToolStripMenuItem1Click(object sender, EventArgs e)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine(string.Format("Peek Poker - Open Source Memory Editor"));
            stringBuilder.AppendLine(string.Format("By"));
            stringBuilder.AppendLine(string.Format("Cybersam"));
            stringBuilder.AppendLine(string.Format("8Ball"));
            stringBuilder.AppendLine(string.Format("PureIso"));
            stringBuilder.AppendLine(string.Format("cornnatron"));
            stringBuilder.AppendLine(string.Format("Special Thanks"));
            stringBuilder.AppendLine(string.Format("Mojobojo"));
            stringBuilder.AppendLine(string.Format("fairchild"));
            stringBuilder.AppendLine(string.Format("360Haven"));
            ShowMessageBox(stringBuilder.ToString(), string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ConverterClearButtonClick(object sender, EventArgs e)
        {
            integer8CalculatorTextBox.Clear();
            integer16CalculatorTextBox.Clear();
            integer32CalculatorTextBox.Clear();
            floatCalculatorTextBox.Clear();
            hexCalculatorTextBox.Clear();
            SetLogText("Conversion Texts Cleared");
        }

        #region button clicks
        //When you click on the connect button
        private void ConnectButtonClick(object sender, EventArgs e)
        {
            try
            {
                SetLogText("Connecting to: " + ipAddressTextBox.Text);
                _rtm = new RealTimeMemory.RealTimeMemory(ipAddressTextBox.Text, 0, 0);//initialize real time memory
                _rtm.ReportProgress += UpdateProgressbar;

                if (!_rtm.Connect())
                {
                    SetLogText("Connecting to " + ipAddressTextBox.Text + " Failed.");
                    throw new Exception("Connection Failed!");
                }
                peeknpoke.Enabled = true;
                searchAndDumpControl.Enabled = true;
                statusStripLabel.Text = String.Format("Connected");
                MessageBox.Show(this, String.Format("Connected"), String.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetLogText("Connected to " + ipAddressTextBox.Text);

                if (!File.Exists(_filepath)) File.Create(_filepath).Dispose(); //Create the file if it doesn't exist
                var objWriter = new StreamWriter(_filepath); //Writer Declaration
                objWriter.Write(ipAddressTextBox.Text); //Writes IP address to text file
                objWriter.Close(); //Close Writer
                connectButton.Text = String.Format("Re-Connect");
                trainersToolStripMenuItem.Enabled = true;
            }
            catch (Exception ex)
            {
                SetLogText("Error: " + ex.Message);
                MessageBox.Show(this, ex.Message, String.Format("Peek Poker"), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        //When you click on the peek button
        private void PeekButtonClick(object sender, EventArgs e)
        {
            AutoComplete();//run function
            try
            {
                if (string.IsNullOrEmpty(peekLengthTextBox.Text))
                    throw new Exception("Invalide peek length!");
                var retValue = Functions.StringToByteArray(_rtm.Peek(peekPokeAddressTextBox.Text, peekLengthTextBox.Text, peekPokeAddressTextBox.Text, peekLengthTextBox.Text));
                var buffer = new Be.Windows.Forms.DynamicByteProvider(retValue) { IsWriteByte = true }; //object initilizer 
                hexBox.ByteProvider = buffer;
                hexBox.Refresh();
                SetLogText("Peeked Address: " + peekPokeAddressTextBox.Text + " Length: " + peekLengthTextBox.Text);
                // the changed are handled automatically with my modifications of Be.HexBox

                MessageBox.Show(this, String.Format("Done!"), String.Format("Peek Poker"), MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetLogText("Error: " + ex.Message);
                MessageBox.Show(this, ex.Message, String.Format("Peek Poker"), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        //When you click on the poke button
        private void PokeButtonClick(object sender, EventArgs e)
        {
            AutoComplete(); //run function
            try
            {
                _rtm.DumpOffset = Functions.Convert(peekPokeAddressTextBox.Text);//Set the dump offset
                _rtm.DumpLength = (uint)hexBox.ByteProvider.Length / 2;//The length of data to dump

                var buffer = (Be.Windows.Forms.DynamicByteProvider)hexBox.ByteProvider;
                SetLogText("Poked Address: " + peekPokeAddressTextBox.Text + " Length: " + _rtm.DumpLength);
                Console.WriteLine(Functions.ByteArrayToString(buffer.Bytes.ToArray()));//?????
                _rtm.Poke(peekPokeAddressTextBox.Text, Functions.ByteArrayToString(buffer.Bytes.ToArray()));
                MessageBox.Show(this, String.Format("Done!"), String.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                SetLogText("Error: " + ex.Message);
                MessageBox.Show(this, ex.Message, String.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //When you click on the www.360haven.com
        private void ToolStripStatusLabel2Click(object sender, EventArgs e)
        {
            SetLogText("URL connection to www.360haven.com");
            Process.Start("www.360haven.com");
        }

        //When you click on the new button
        private void NewPeekButtonClick(object sender, EventArgs e)
        {
            NewPeek();
            SetLogText("New Peek Initiated!");
        }

        //Esearch Button
        private void SearchRangeButtonClick(object sender, EventArgs e)
        {
            try
            {
                if (searchRangeEndTypeCB.SelectedIndex == 1)
                {
                    _searchRangeDumpLength = (Functions.Convert(endRangeAddressTextBox.Text) - Functions.Convert(startRangeAddressTextBox.Text));
                }
                else
                {
                    _searchRangeDumpLength = Functions.Convert(endRangeAddressTextBox.Text);
                }
                var oThread = new Thread(SearchRange);
                oThread.Start();
            }
            catch (Exception ex)
            {
                ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //When you click on an item on the search range result list view - Search Range tab
        private void SearchRangeResultListViewMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return; //if its not a left click return
            peekPokeAddressTextBox.Text = string.Format("0x" + resultGrid.Rows[resultGrid.SelectedRows[0].Index].Cells[1].Value);
        }
        
        private void ResultGridCellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            var cell = (DataGridCell)sender;
            if (resultGrid.Rows[cell.RowNumber].Cells[2].Value != null)
                resultGrid.Rows[cell.RowNumber].DefaultCellStyle.ForeColor = System.Drawing.Color.Red;
        }

        // Refresh results
        private void ResultRefreshClick(object sender, EventArgs e)
        {
            if (_searchResult.Count > 0)
            {
                var thread = new Thread(RefreshResultList);
                //thread.Name = "RefreshResultsList";
                thread.Start();
            }
            else
            {
                ShowMessageBox("Can not refresh! \r\n Resultlist empty!!", string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        //stop the search
        private void StopSearchButtonClick(object sender, EventArgs e)
        {
            SetLogText("Searching was stopped!");
            _rtm.StopSearch = true;
        }

        private void DumpMemoryButtonClick(object sender, EventArgs e)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog();
                if (saveFileDialog.ShowDialog() != DialogResult.OK) return;
                _dumpFilePath = saveFileDialog.FileName;
                FileStream file = File.Create(_dumpFilePath);
                file.Close();
                SetLogText("Dump Memory to: " + _dumpFilePath);

                var oThread = new Thread(Dump);
                oThread.Start();
            }
            catch (Exception ex)
            {
                SetLogText("Dump Error: " + ex.Message);
                ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion

        #region HexBox Events
        private void HexBoxSelectionStartChanged(object sender, EventArgs e)
        {
            ChangeNumericValue();//When you select an offset on the hexbox

            var prev = Functions.HexToBytes(peekPokeAddressTextBox.Text);
            var address = Functions.BytesToInt32(prev);
            SelAddress.Text = string.Format("0x" + (address + (int)hexBox.SelectionStart).ToString("X8"));
        }
        private void IsSignedCheckedChanged(object sender, EventArgs e)
        {
            ChangeNumericMaxMin();
            ChangeNumericValue();
        }
        private void NumericIntKeyPress(object sender, KeyPressEventArgs e)
        {
            if (hexBox.ByteProvider != null)
            {
                ChangedNumericValue(sender);
            }
        }
        #endregion

        #region Search Tab Events
        private void SearchRangeValueTextBoxKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Return || !searchRangeValueTextBox.Focused) return;

            if (searchRangeEndTypeCB.SelectedIndex == 1)
            {
                _searchRangeDumpLength = (Functions.Convert(endRangeAddressTextBox.Text) - Functions.Convert(startRangeAddressTextBox.Text));
            }
            else
            {
                _searchRangeDumpLength = Functions.Convert(endRangeAddressTextBox.Text);
            }

            var oThread = new Thread(SearchRange);
            oThread.Start();
            e.Handled = true;
            searchRangeButton.Focus();
        }
        #endregion

        #region functions
        private void NewPeek()
        {
            //Clean up
            peekPokeAddressTextBox.Clear();
            peekLengthTextBox.Clear();
            hexBox.ByteProvider = null;
            hexBox.Refresh();
        }
        private void AutoComplete()
        {
            peekPokeAddressTextBox.AutoCompleteCustomSource = _data;//put the auto complete data into the textbox
            var count = _data.Count;
            for (var index = 0; index < count; index++)
            {
                var value = _data[index];
                //if the text in peek or poke text box is not in autocomplete data - Add it
                if (!ReferenceEquals(value, peekPokeAddressTextBox.Text))
                    _data.Add(peekPokeAddressTextBox.Text);
            }
        }

        //When you select an offset on the hexbox
        private void ChangeNumericValue()
        {
            if (hexBox.ByteProvider == null) return;
            var buffer = hexBox.ByteProvider.Bytes;
            if (isSigned.Checked)
            {
                NumericInt8.Value = (buffer.Count - hexBox.SelectionStart) > 0 ?
                    Functions.ByteToSByte(hexBox.ByteProvider.ReadByte(hexBox.SelectionStart)) : 0;
                NumericInt16.Value = (buffer.Count - hexBox.SelectionStart) > 1 ?
                    Functions.BytesToInt16(buffer.GetRange((int)hexBox.SelectionStart, 2).ToArray()) : 0;
                NumericInt32.Value = (buffer.Count - hexBox.SelectionStart) > 3 ?
                    Functions.BytesToInt32(buffer.GetRange((int)hexBox.SelectionStart, 4).ToArray()) : 0;
            }
            else
            {
                NumericInt8.Value = (buffer.Count - hexBox.SelectionStart) > 0 ?
                    buffer[(int)hexBox.SelectionStart] : 0;
                NumericInt16.Value = (buffer.Count - hexBox.SelectionStart) > 1 ?
                    Functions.BytesToUInt16(buffer.GetRange((int)hexBox.SelectionStart, 2).ToArray()) : 0;
                NumericInt32.Value = (buffer.Count - hexBox.SelectionStart) > 3 ?
                    Functions.BytesToUInt32(buffer.GetRange((int)hexBox.SelectionStart, 4).ToArray()) : 0;
            }
            var prev = Functions.HexToBytes(peekPokeAddressTextBox.Text);
            var address = Functions.BytesToInt32(prev);
            SelAddress.Text = string.Format("0x" + (address + (int)hexBox.SelectionStart).ToString("X8"));
        }
        private void ChangedNumericValue(object sender)
        {
            if (hexBox.SelectionStart >= hexBox.ByteProvider.Bytes.Count) return;
            var numeric = (NumericUpDown)sender;
            switch (numeric.Name)
            {
                case "NumericInt8":
                    if (isSigned.Checked)
                    {
                        Console.WriteLine(((sbyte)numeric.Value).ToString("X2"));
                        hexBox.ByteProvider.WriteByte(hexBox.SelectionStart,
                                                      Functions.HexToBytes(((sbyte)numeric.Value).ToString("X2"))[0]);
                    }
                    else
                    {
                        hexBox.ByteProvider.WriteByte(hexBox.SelectionStart,
                                                      Convert.ToByte((byte)numeric.Value));
                    }
                    break;
                case "NumericInt16":
                    for (var i = 0; i < 2; i++)
                    {
                        hexBox.ByteProvider.WriteByte(hexBox.SelectionStart + i, isSigned.Checked
                                                                                    ? Functions.Int16ToBytes((short)numeric.Value)[i]
                                                                                    : Functions.UInt16ToBytes((ushort)numeric.Value)[i]);
                    }
                    break;
                case "NumericInt32":
                    for (var i = 0; i < 4; i++)
                    {
                        hexBox.ByteProvider.WriteByte(hexBox.SelectionStart + i, isSigned.Checked
                                                                                    ? Functions.Int32ToBytes((int)numeric.Value)[i]
                                                                                    : Functions.UInt32ToBytes((uint)numeric.Value)[i]);
                    }
                    break;
            }
            hexBox.Refresh();
        }
        private void ChangeNumericMaxMin()
        {
            if (isSigned.Checked)
            {
                NumericInt8.Maximum = SByte.MaxValue;
                NumericInt8.Minimum = SByte.MinValue;
                NumericInt16.Maximum = Int16.MaxValue;
                NumericInt16.Minimum = Int16.MinValue;
                NumericInt32.Maximum = Int32.MaxValue;
                NumericInt32.Minimum = Int32.MinValue;
            }
            else
            {
                NumericInt8.Maximum = Byte.MaxValue;
                NumericInt8.Minimum = Byte.MinValue;
                NumericInt16.Maximum = UInt16.MaxValue;
                NumericInt16.Minimum = UInt16.MinValue;
                NumericInt32.Maximum = UInt32.MaxValue;
                NumericInt32.Minimum = UInt32.MinValue;
            }
        }

        private void Injectcodes()
        {
            if (!File.Exists(_trainerdottext)) return;
            try
            {
                //trainersToolStripMenuItem.DropDownItems.Add(k);

                string trainerinjectorcode = File.ReadAllText(_trainerdottext);
                var trainereader = new StringReader(trainerinjectorcode);

                //Read Game Name
                string name = trainereader.ReadLine();
                do
                {
                    if (name == "#")
                        break;

                    var k = new ToolStripMenuItem();
                    if (name != null) k.Text = name.Substring(1, (name.Length - 1));

                    string id = trainereader.ReadLine();
                    string titleUpdate = trainereader.ReadLine();

                    string code = trainereader.ReadLine();
                    do
                    {
                        if (code != null && code.Substring(0, 1) == "#")
                            break;
                        if (name == "#")
                            break;

                        var j = new ToolStripMenuItem();
                        var codes = new List<Types.CodeList>();
                        var cLine = trainereader.ReadLine();
                        do
                        {
                            if (cLine != null && cLine.Substring(0, 1) == "#")
                                break;
                            if (name == "#")
                                break;

                            Types.CodeList NCode = new Types.CodeList();
                            NCode.Name = code.Substring(1, (code.Length - 1));
                            NCode.Type = Convert.ToInt32(cLine.Substring(0, 1));
                            NCode.Adress = Functions.BytesToUInt32(Functions.StringToByteArray(cLine.Substring(2, 8)));
                            NCode.Code = Functions.BytesToUInt32(Functions.StringToByteArray(cLine.Substring(11, 8)));
                            codes.Add(NCode);

                            cLine = trainereader.ReadLine();
                        } while (cLine.Substring(0, 1) != "$");

                        j.Text = code.Substring(1, (code.Length - 1));
                        j.Tag = codes;
                        j.Click += Codesmith; //Event adder
                        k.DropDownItems.Add(j);
                        code = cLine;

                    } while (code.Substring(0, 1) == "$");

                    trainersToolStripMenuItem.DropDownItems.Add(k);
                    name = code;

                    if (name == "#")
                        break;

                } while (name.Substring(0, 1) == "#");
            }
            catch (Exception ex)
            {
                SetLogText("Inject Code error: " + ex.Message);
                MessageBox.Show(ex.Message);
            }
        }

        private void Codesmith(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;             // get the menu item
            List<Types.CodeList> Codes = (List<Types.CodeList>)item.Tag;    // get the code list

            // read each entry and write the code to memory
            foreach (Types.CodeList code in Codes)
            {
                uint currentaddress = code.Adress;
                string currentvalue = Functions.ToHexString(Functions.UInt32ToBytes(code.Code));
                _rtm.WriteMemory(currentaddress, currentvalue);
            }
        }
        #endregion

        #region Thread Functions
        //Refresh results Thread
        private void RefreshResultList()
        {
            try
            {
                var value = 0;
                foreach (var item in _searchResult)
                {
                    UpdateProgressbar(0, _searchResult.Count, value, "Refreshing...");
                    value++;

                    //peekPokeAddressTextBox.Text = "0x" + _item.Offset;
                    var length = (item.Value.Length / 2).ToString("X");
                    var retvalue = _rtm.Peek("0x" + item.Offset, length, "0x" + item.Offset, length);

                    if (item.Value == retvalue) continue;//if value hasn't change continue foreach loop

                    GridRowColours(value);
                    item.Value = retvalue;
                    SetLogText("Search List was refreshed!");
                }

                ResultGridUpdate();
                UpdateProgressbar(0, 100, 0, "idle");
            }
            catch (Exception ex)
            {
                SetLogText("Search List Error: " + ex.Message);
                ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Thread.CurrentThread.Abort();
            }
        }

        //Searches the memory for the specified value (Experimental)
        private void SearchRange()
        {
            try
            {
                EnableSearchRangeButton(false);
                EnableExSearchRangeButton(false);
                EnableStopSearchButton(true);
                SetLogText("Search Offset: " + GetStartRangeAddressTextBoxText() + " Search Length: " +
                           _searchRangeDumpLength);
                _rtm.DumpOffset = Functions.Convert(GetStartRangeAddressTextBoxText());
                _rtm.DumpLength = _searchRangeDumpLength;

                ResultGridClean();//Clean list view

                //The ExFindHexOffset function is a Experimental search function
                var results = _rtm.FindHexOffset(GetSearchRangeValueTextBoxText());//pointer
                //Reset the progressbar...
                UpdateProgressbar(0, 100, 0);

                if (results.Count < 1)
                {
                    ShowMessageBox(string.Format("No result/s found!"), string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return; //We don't want it to continue
                }
                _searchResult = results;
                ResultGridUpdate();
            }
            catch (Exception e)
            {
                SetLogText("Search Range Error: " + e.Message);
                ShowMessageBox(e.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EnableExSearchRangeButton(true);
                EnableSearchRangeButton(true);
                EnableStopSearchButton(false);
                Thread.CurrentThread.Abort();
            }
        }

        //Dump memory to a file
        private void Dump()
        {
            try
            {
                EnableDumpButton(false);
                SetLogText("Dump Offset: " + GetDumpStartOffsetTextBoxText() + " Dump Length: " + GetDumpLengthTextBox());
                _rtm.Dump(_dumpFilePath, GetDumpStartOffsetTextBoxText(), GetDumpLengthTextBox());
            }
            catch (Exception e)
            {
                SetLogText("Dump Error: "+e.Message);
                ShowMessageBox(e.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EnableDumpButton(true);
                Thread.CurrentThread.Abort();
            }
        }
        #endregion

        #region safeThreadingProperties
        //==================================================================
        // This method demonstrates a pattern for making thread-safe
        // calls on a Windows Forms control. 
        //
        // If the calling thread is different from the thread that
        // created the TextBox control, this method creates a
        // Set/Get Method and calls itself asynchronously using the
        // Invoke method.
        //
        // If the calling thread is the same as the thread that created
        // the TextBox control, the Text property is set directly. 
        //Reference: http://msdn.microsoft.com/en-us/library/ms171728.aspx
        //===================================================================

        //Get and Set values
        private String GetSearchRangeValueTextBoxText()//Get the value from the textbox - safe
        {
            //recursion
            var returnVal = "";
            if (searchRangeValueTextBox.InvokeRequired) searchRangeValueTextBox.Invoke((MethodInvoker)
                  delegate { returnVal = GetSearchRangeValueTextBoxText(); });
            else
                return searchRangeValueTextBox.Text;
            return returnVal;
        }
        private String GetDumpStartOffsetTextBoxText()//Get the value from the textbox - safe
        {
            //recursion
            var returnVal = "";
            if (dumpStartOffsetTextBox.InvokeRequired) dumpStartOffsetTextBox.Invoke((MethodInvoker)
                  delegate { returnVal = GetDumpStartOffsetTextBoxText(); });
            else
                return dumpStartOffsetTextBox.Text;
            return returnVal;
        }
        private String GetDumpLengthTextBox()//Get the value from the textbox - safe
        {
            //recursion
            var returnVal = "";
            if (dumpLengthTextBox.InvokeRequired) dumpLengthTextBox.Invoke((MethodInvoker)
                  delegate { returnVal = GetDumpLengthTextBox(); });
            else
                return dumpLengthTextBox.Text;
            return returnVal;
        }
        private string GetStartRangeAddressTextBoxText()
        {
            //recursion
            var returnVal = "";
            if (startRangeAddressTextBox.InvokeRequired) startRangeAddressTextBox.Invoke((MethodInvoker)
                  delegate { returnVal = GetStartRangeAddressTextBoxText(); });
            else
                return startRangeAddressTextBox.Text;
            return returnVal;
        }
        private void EnableStopSearchButton(bool value)
        {
            if (stopSearchButton.InvokeRequired)
                stopSearchButton.Invoke((MethodInvoker)delegate { EnableStopSearchButton(value); });
            else
                stopSearchButton.Enabled = value;
        }
        private void EnableDumpButton(bool value)
        {
            if (dumpMemoryButton.InvokeRequired)
                dumpMemoryButton.Invoke((MethodInvoker)delegate { EnableDumpButton(value); });
            else
                dumpMemoryButton.Enabled = value;
        }
        private void EnableSearchRangeButton(bool value)
        {
            if (searchRangeButton.InvokeRequired)
                searchRangeButton.Invoke((MethodInvoker)delegate { EnableSearchRangeButton(value); });
            else
                searchRangeButton.Enabled = value;
        }
        private void EnableExSearchRangeButton(bool value)
        {
            if (searchRangeButton.InvokeRequired)
                searchRangeButton.Invoke((MethodInvoker)delegate { EnableExSearchRangeButton(value); });
            else
                searchRangeButton.Enabled = value;
        }
        private void SetLogText(string value)
        {
            if (logTextBox.InvokeRequired)
                Invoke((MethodInvoker)(() => SetLogText(value)));
            else
            {
                var m = DateTime.Now.ToString("HH:mm:ss tt") + " " + value + Environment.NewLine;
                logTextBox.Text += m;
                logTextBox.Select(logTextBox.Text.Length, 0); // set the cursor to end of textbox
                logTextBox.ScrollToCaret();                     // scroll down to the cursor position
            }
        }
        private void ShowMessageBox(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            //Using lambda express - I believe its slower - Just an example
            if (InvokeRequired)
                Invoke((MethodInvoker)(() => ShowMessageBox(text, caption, buttons, icon)));
            else MessageBox.Show(this, text, caption, buttons, icon);
        }

        //Control changes
        private void GridRowColours(int value)
        {
            if (resultGrid.InvokeRequired)
                resultGrid.Invoke((MethodInvoker)delegate { GridRowColours(value); });
            else
                resultGrid.Rows[value - 1].DefaultCellStyle.ForeColor = System.Drawing.Color.Red;
        }

        //Refresh the values of Search Results
        private void ResultGridClean()
        {
            if (resultGrid.InvokeRequired)
                resultGrid.Invoke((MethodInvoker)(ResultGridClean));
            else
                resultGrid.Rows.Clear();
        }
        private void ResultGridUpdate()
        {
            //IList or represents a collection of objects(String)
            if (resultGrid.InvokeRequired)
                //lambda expression empty delegate that calls a recursive function if InvokeRequired
                resultGrid.Invoke((MethodInvoker)(ResultGridUpdate));
            else
            {
                resultGrid.DataSource = _searchResult;
                resultGrid.Refresh();
            }
        }

        //Progressbar Delegates
        private void UpdateProgressbar(int min, int max, int value, string text = "Idle")
        {
            if (statusStrip1.InvokeRequired)
            {
                statusStrip1.Invoke((MethodInvoker)(() => UpdateProgressbar(min, max, value, text)));
            }
            else
            {
                if (StatusProgressBar.ProgressBar != null)
                {
                    StatusProgressBar.ProgressBar.Maximum = max;
                    StatusProgressBar.ProgressBar.Minimum = min;
                    StatusProgressBar.ProgressBar.Value = value;
                }
                statusStripLabel.Text = text;
            }


        }
        #endregion

        #region Addressbox Autocorrection
        // These will automatically add "0x" to an offset if it hasn't been added already - 8Ball
        private void FixTheAddresses(object sender, EventArgs e)
        {
            if (!peekPokeAddressTextBox.Text.StartsWith("0x")) //Peek Address Box, Formatting Check.
            {//PeekPokeAddress
                if (!peekPokeAddressTextBox.Text.Equals("")) //Empty Check
                    peekPokeAddressTextBox.Text = (string.Format("0x" + peekPokeAddressTextBox.Text)); //Formatting
            }
            if (peekLengthTextBox.Text.StartsWith("0x")) // Checks if peek length is hex value or not based on 0x
            { //Peeklength pt1
                string result = (peekLengthTextBox.Text.ToUpper().Substring(2));
                uint result2 = UInt32.Parse(result, System.Globalization.NumberStyles.HexNumber);
                peekLengthTextBox.Text = result2.ToString();
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(peekLengthTextBox.Text.ToUpper(), "^[A-Z]$")) //Checks if hex, based on uppercase alphabet presence.
            {//Peeklength pt2
                string result = (peekLengthTextBox.Text.ToUpper());
                uint result2 = UInt32.Parse(result, System.Globalization.NumberStyles.HexNumber);
                peekLengthTextBox.Text = result2.ToString();
            }
            else if (peekLengthTextBox.Text.StartsWith("h")) //Checks if hex, based on starting with h.
            {//Peeklength pt3
                string result = (peekLengthTextBox.Text.ToUpper().Substring(1));
                uint result2 = UInt32.Parse(result, System.Globalization.NumberStyles.HexNumber);
                peekLengthTextBox.Text = result2.ToString();
            }
            if (!startRangeAddressTextBox.Text.StartsWith("0x"))
            {//RangeStart
                if (!startRangeAddressTextBox.Text.Equals(""))
                    startRangeAddressTextBox.Text = (string.Format("0x" + startRangeAddressTextBox.Text));
            }
            if (endRangeAddressTextBox.Text.StartsWith("0x")) return; //RangeEnd
            if (!endRangeAddressTextBox.Text.Equals(""))
                endRangeAddressTextBox.Text = (string.Format("0x" + endRangeAddressTextBox.Text));
        }

        private void FixDumpAddresses(object sender, EventArgs e)
        {
            if (dumpStartOffsetTextBox.Text.StartsWith("0x")) return;
            if (dumpStartOffsetTextBox.Text.Equals("")) return;
            dumpStartOffsetTextBox.Text = (string.Format("0x" + dumpStartOffsetTextBox.Text));
            if (!System.Text.RegularExpressions.Regex.IsMatch(dumpStartOffsetTextBox.Text.Substring(2), @"\A\b[0-9a-fA-F]+\b\Z"))
                dumpStartOffsetTextBox.Clear();
        }

        private void FixDumpLength(object sender, EventArgs e)
        {
            if (dumpLengthTextBox.Text.StartsWith("0x")) return;
            if (dumpLengthTextBox.Text.Equals("")) return;
            dumpLengthTextBox.Text = (string.Format("0x" + dumpLengthTextBox.Text));
            if (!System.Text.RegularExpressions.Regex.IsMatch(dumpLengthTextBox.Text.Substring(2), @"\A\b[0-9a-fA-F]+\b\Z"))
                dumpLengthTextBox.Clear();
        }

        #endregion

        #region Autocalculation
        private void Int32ToHex(object sender, EventArgs e)
        {
            if (!integer32CalculatorTextBox.Focused) return; //if the integer textbox isn't selected return
            if (System.Text.RegularExpressions.Regex.IsMatch(integer32CalculatorTextBox.Text.ToUpper(), "[A-Z]")) return; //if we have characters return

            Int32 number;
            var validResult = Int32.TryParse(integer32CalculatorTextBox.Text, out number); //Stops things like a single "-" causing errors
            if (!validResult) return;

            
            if (!BigEndianRadioButton.Checked)
            {
                var num = number & 0xff;
                var num2 = (number >> 8) & 0xff;
                var num3 = (number >> 0x10) & 0xff;
                var num4 = (number >> 0x18) & 0xff;
                number = ((((num << 0x18) | (num2 << 0x10)) | (num3 << 8)) | num4);
            }

            var hex = number.ToString("X4"); //x is for hex and 4 is padding to a 4 digit value, uppercases.
            hexCalculatorTextBox.Text = (string.Format("0x" + hex)); //Formats string, adds 0x
        }

        private void Int8ToHex(object sender, EventArgs e)
        {
            if (!integer8CalculatorTextBox.Focused) return; //if the integer textbox isn't selected return
            if (System.Text.RegularExpressions.Regex.IsMatch(integer8CalculatorTextBox.Text.ToUpper(), "[A-Z]")) return; //if we have characters return
            
            byte number;
            var validResult = byte.TryParse(integer8CalculatorTextBox.Text, out number);
            if (!validResult) return;

            var hex = number.ToString("X2"); //x is for hex and 2 is padding to a 2 digit value, uppercases.
            hexCalculatorTextBox.Text = (string.Format("0x" + hex)); //Formats string, adds 0x
        }

        private void Int16ToHex(object sender, EventArgs e)
        {
            if (!integer16CalculatorTextBox.Focused) return; //if the integer textbox isn't selected return
            if (System.Text.RegularExpressions.Regex.IsMatch(integer16CalculatorTextBox.Text.ToUpper(), "[A-Z]")) return; //if we have characters return
            
            short number;
            var validResult = short.TryParse(integer16CalculatorTextBox.Text, out number);
            if (!validResult) return;

            if (!BigEndianRadioButton.Checked)
            {
                var num = number & 0xff;
                var num2 = (number >> 8) & 0xff;
                number = (short)((num << 8) | num2);
            }
            hexCalculatorTextBox.Text = (string.Format("0x" + number.ToString("X3")));
        }

        private void FloatToHex(object sender, EventArgs e)
        {
            if (!floatCalculatorTextBox.Focused) return; //if the integer textbox isn't selected return
            if (System.Text.RegularExpressions.Regex.IsMatch(floatCalculatorTextBox.Text.ToUpper(), "[A-Z]")) return; //if we have characters return

            float number;
            var validResult = float.TryParse(floatCalculatorTextBox.Text, out number);
            if (!validResult) return;

            var buffer = BitConverter.GetBytes(number);//comes out as little endian
            if (BigEndianRadioButton.Checked) Array.Reverse(buffer);

            var hex = BitConverter.ToString(buffer).Replace("-", "");
            hexCalculatorTextBox.Text = (string.Format("0x" + hex));
        }

        private void HexToInt(object sender, EventArgs e)
        {
            if (!hexCalculatorTextBox.Focused) return;
            var hexycalc = hexCalculatorTextBox.Text.StartsWith("0x") ? hexCalculatorTextBox.Text.Substring(2) : hexCalculatorTextBox.Text;
            
            if (!System.Text.RegularExpressions.Regex.IsMatch(hexycalc, @"\A\b[0-9a-fA-F]+\b\Z")) return;
            try
            {
                if (hexycalc.Length >= 0 && hexycalc.Length <= 2)
                {
                    integer8CalculatorTextBox.Text = Convert.ToSByte(hexCalculatorTextBox.Text, 16).ToString();
                    integer16CalculatorTextBox.Clear();
                    integer32CalculatorTextBox.Clear();
                    floatCalculatorTextBox.Clear();
                }
                if (hexycalc.Length >= 2 && hexycalc.Length <= 4)
                {
                    var number = Convert.ToInt16(hexCalculatorTextBox.Text, 16);
                    if (!BigEndianRadioButton.Checked)
                    {
                        var num = number & 0xff;
                        var num2 = (number >> 8) & 0xff;
                        number = (short)((num << 8) | num2);
                    }

                    integer16CalculatorTextBox.Text = number.ToString();
                    integer32CalculatorTextBox.Clear();
                    floatCalculatorTextBox.Clear();
                }
                if (hexycalc.Length >= 8)
                {
                    var number = Convert.ToInt32(hexCalculatorTextBox.Text, 16);
                    if (!BigEndianRadioButton.Checked)
                    {
                        var num = number & 0xff;
                        var num2 = (number >> 8) & 0xff;
                        var num3 = (number >> 0x10) & 0xff;
                        var num4 = (number >> 0x18) & 0xff;
                        number = ((((num << 0x18) | (num2 << 0x10)) | (num3 << 8)) | num4);
                    }
                    integer32CalculatorTextBox.Text = number.ToString();

                    var input = hexCalculatorTextBox.Text;
                    var output = new byte[(input.Length / 2)];

                    if ((input.Length % 2) != 0) input = "0" + input;
                    int index;
                    for (index = 0; index < output.Length; index++)
                    {
                        output[index] = Convert.ToByte(input.Substring((index * 2), 2), 16);
                    }
                    Array.Reverse(output);
                    floatCalculatorTextBox.Text = BitConverter.ToSingle(output, 0).ToString();
                }
            }
            catch(Exception ex)
            {
                SetLogText("Suppressed Conversion Error: " + ex.Message);
            }
        }
        #endregion

        #region Trainers
        #region Skyrim
        //Skyrim TU#4/5
        // Inf Stamina
        private void SkyrimInfSprint(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Skyrim - TU#4/5 - Infinite Sprint - Sent");
            try
            {
                _rtm.WriteMemory(0x834F9890, "00000000");
                _rtm.WriteMemory(0x834F9650, "00000000");
                _rtm.WriteMemory(0x834FB234, "00000000");
                _rtm.WriteMemory(0x834FB24C, "00000000");
            }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Inf Mana  
        private void SkyrimInfMagicka(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Skyrim - TU#4/5 - Infinite Stamina - Sent");
            try
            { _rtm.WriteMemory(0x834FB234, "00000000"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        #endregion
        #region DarkSouls
        //Dark Souls TU#0/1
        // Max Level
        private void Ds0MaxLevel(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Level - Sent");
            try
            { _rtm.WriteMemory(0xC95A2108, "000002C8"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Souls 
        private void Ds0MaxSouls(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Souls - Sent");
            try
            { _rtm.WriteMemory(0xC95A210C, "3B9AC9FF"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Humanity 
        private void Ds0Humanity(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Humanity - Sent");
            try
            { _rtm.WriteMemory(0xC95A20FC, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Vitality           
        private void Ds0Vitality(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Vitality - Sent");
            try
            { _rtm.WriteMemory(0xC95A20B8, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Attunement
        private void Ds0Attunement(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Attunement - Sent");
            try
            { _rtm.WriteMemory(0xC95A20C0, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Intelligence
        private void Ds0Intelligence(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Intelligence- Sent");
            try
            { _rtm.WriteMemory(0xC95A20E0, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Resistance 
        private void Ds0Resistance(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Resistance - Sent");
            try
            { _rtm.WriteMemory(0xC95A2100, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Dexterity
        private void Ds0Dexterity(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Dexterity - Sent");
            try
            { _rtm.WriteMemory(0xC95A20D8, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Faith  
        private void Ds0Faith(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Faith - Sent");
            try
            { _rtm.WriteMemory(0xC95A20E8, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Stamina 
        private void Ds0Stamina(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Stamina  - Sent");
            try
            { _rtm.WriteMemory(0xC95A20B0, "000000A0"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Stamina 1 Million 
        private void Ds0MillionStamina(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - 1 Million Stamina  - Sent");
            try
            { _rtm.WriteMemory(0xC95A20B0, "3B9ACA00"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Strength  
        private void Ds0Strength(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Strength  - Sent");
            try
            { _rtm.WriteMemory(0xC95A20D0, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Max Endurance 
        private void Ds0Endurance(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - Max Endurance  - Sent");
            try
            { _rtm.WriteMemory(0xC95A20C8, "00000063"); }
            catch { SetLogText("Error! Could not poke code."); }
        }
        // Endurance 1 Million
        private void Ds0MillionEndurance(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Dark Souls - TU#0/1 - 1 Million Endurance  - Sent");
            try
            {
                _rtm.WriteMemory(0xC95A20C8, "3B9ACA00");
            }
            catch { SetLogText("Error! Could not poke code."); }
        }
        private void Ds0All(object sender, EventArgs e)
        {
            Ds0MaxLevel(DS0MaxLevel, System.EventArgs.Empty);
            Ds0MaxSouls(DS0MaxSouls, System.EventArgs.Empty);
            Ds0Vitality(DS0MaxVit, System.EventArgs.Empty);
            Ds0Endurance(DS0MaxEnd, System.EventArgs.Empty);
            Ds0Attunement(DS0MaxAtt, System.EventArgs.Empty);
            Ds0Strength(DS0MaxStr, System.EventArgs.Empty);
            Ds0Dexterity(DS0MaxDex, System.EventArgs.Empty);
            Ds0Resistance(DS0MaxRes, System.EventArgs.Empty);
            Ds0Intelligence(DS0MaxInt, System.EventArgs.Empty);
            Ds0Faith(DS0MaxFaith, System.EventArgs.Empty);
            Ds0Humanity(DS0MaxHum, System.EventArgs.Empty);
            Ds0Stamina(DS0MaxStam, System.EventArgs.Empty);
        }
        #endregion
        #region Resonce Of Fate
        private void ResonanceOfFateMenuItemClick(object sender, EventArgs e)
        {
            var menu = (ToolStripMenuItem)sender;
            try
            {
                var oThread = new Thread(ExROF);
                oThread.Start(menu.Tag);
            }
            catch (Exception ex)
            {
                ShowMessageBox(ex.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private void ExROF(object sets)
        {
            List<string> _poke;
            #region List Values
            List<string> WhiteHex = new List<string>();
            List<string> ColorHex = new List<string>();
            List<string> HexStations = new List<string>();
            List<string> WeaponSet1 = new List<string>();
            List<string> WeaponSet2 = new List<string>();
            List<string> WeaponDebugSet = new List<string>();
            List<string> ItemSpecialSet1 = new List<string>();
            List<string> ItemSpecialSet2 = new List<string>();

            #region White Hex
            WhiteHex.Add("0001010000000001000003E7000003E700000000");
            WhiteHex.Add("0002010000000001000003E7000003E700000000");
            WhiteHex.Add("0003010000000001000003E7000003E700000000");
            WhiteHex.Add("0004010000000001000003E7000003E700000000");
            WhiteHex.Add("0005010000000001000003E7000003E700000000");
            WhiteHex.Add("0006010000000001000003E7000003E700000000");
            WhiteHex.Add("0007010000000001000003E7000003E700000000");
            WhiteHex.Add("0008010000000001000003E7000003E700000000");
            WhiteHex.Add("0009010000000001000003E7000003E700000000");
            WhiteHex.Add("000A010000000001000003E7000003E700000000");
            #endregion
            #region Colored Hex
            ColorHex.Add("000B010000000001000003E7000003E700000000");
            ColorHex.Add("0010010000000001000003E7000003E700000000");
            ColorHex.Add("0015010000000001000003E7000003E700000000");
            ColorHex.Add("001A010000000001000003E7000003E700000000");
            ColorHex.Add("001F010000000001000003E7000003E700000000");
            ColorHex.Add("0024010000000001000003E7000003E700000000");
            ColorHex.Add("0029010000000001000003E7000003E700000000");
            ColorHex.Add("002E010000000001000003E7000003E700000000");
            ColorHex.Add("0033010000000001000003E7000003E700000000");
            ColorHex.Add("0038010000000001000003E7000003E700000000");
            ColorHex.Add("003D010000000001000003E7000003E700000000");
            ColorHex.Add("003E010000000001000003E7000003E700000000");
            #endregion
            #region Hex Stations
            HexStations.Add("0042010000000001000003E7000003E700000000");
            HexStations.Add("0043010000000001000003E7000003E700000000");
            HexStations.Add("0044010000000001000003E7000003E700000000");
            HexStations.Add("0045010000000001000003E7000003E700000000");
            HexStations.Add("0046010000000001000003E7000003E700000000");
            HexStations.Add("0047010000000001000003E7000003E700000000");
            HexStations.Add("0048010000000001000003E7000003E700000000");
            HexStations.Add("0049010000000001000003E7000003E700000000");
            HexStations.Add("004A010000000001000003E7000003E700000000");
            HexStations.Add("004B010000000001000003E7000003E700000000");
            HexStations.Add("004C010000000001000003E7000003E700000000");
            #endregion
            #region Weapon Set 1
            WeaponSet1.Add("03F0010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F1010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F2010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F3010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F4010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F5010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F6010000000001000003E7000003E700000000");
            WeaponSet1.Add("03F7010000000001000003E7000003E700000000");
            #endregion
            #region Weapon Set 2
            WeaponSet2.Add("03FE010000000001000003E7000003E700000000");
            WeaponSet2.Add("0400010000000001000003E7000003E700000000");
            WeaponSet2.Add("0401010000000001000003E7000003E700000000");
            WeaponSet2.Add("0402010000000001000003E7000003E700000000");
            WeaponSet2.Add("0403010000000001000003E7000003E700000000");
            WeaponSet2.Add("0404010000000001000003E7000003E700000000");
            WeaponSet2.Add("0405010000000001000003E7000003E700000000");
            WeaponSet2.Add("0406010000000001000003E7000003E700000000");
            WeaponSet2.Add("0407010000000001000003E7000003E700000000");
            WeaponSet2.Add("0408010000000001000003E7000003E700000000");
            WeaponSet2.Add("0409010000000001000003E7000003E700000000");
            #endregion
            #region Weapon Debug Set
            WeaponDebugSet.Add("03F8010000000001000003E7000003E700000000");
            WeaponDebugSet.Add("03F9010000000001000003E7000003E700000000");
            WeaponDebugSet.Add("03FA010000000001000003E7000003E700000000");
            #endregion
            #region Item Special Set 1
            ItemSpecialSet1.Add("0528010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("0471010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("046B010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("046A010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F0010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F1010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F2010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F8010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F9010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07FA010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("0464010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07FB010000000001000003E7000003E700000000");
            ItemSpecialSet1.Add("07F3010000000001000003E7000003E700000000");
            #endregion
            #region Item Special Set 2
            ItemSpecialSet2.Add("0566010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("0567010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("0562010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("07FC010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("07FD010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("03F4010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("0561010000000001000003E7000003E700000000");
            ItemSpecialSet2.Add("04E0010000000001000003E7000003E700000000");
            #endregion
            #endregion
            #region Switch
            switch ((string)sets)
            {
                case "WhiteHex":
                    _poke = WhiteHex;
                    break;
                case "ColorHex":
                    _poke = ColorHex;
                    break;
                case "HexStations":
                    _poke = HexStations;
                    break;
                case "WeaponSet1":
                    _poke = WeaponSet1;
                    break;
                case "WeaponSet2":
                    _poke = WeaponSet2;
                    break;
                case "WeaponDebugSet":
                    _poke = WeaponDebugSet;
                    break;
                case "ItemSpecialSet1":
                    _poke = ItemSpecialSet1;
                    break;
                case "ItemSpecialSet2":
                    _poke = ItemSpecialSet2;
                    break;

                default:
                    _poke = WhiteHex;
                    break;
            }
            #endregion

            #region Dump/Search and Poke
            try
            {
                CheckForIllegalCrossThreadCalls = false; //line 476 grid cross thread error
                _rtm.DumpOffset = 0xCD500000;
                _rtm.DumpLength = 0x500000;

                SetLogText("#Trainers# Resonance Of Fate Dumping & Searching ...");
                //The ExFindHexOffset function is a Experimental search function
                var results = _rtm.FindHexOffset("04B10100000003E8");
                //Reset the progressbar...
                UpdateProgressbar(0, 100, 0);

                if (results.Count < 1)
                {
                    ShowMessageBox(string.Format("No result/s found!"), string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return; //We don't want it to continue
                }

                UpdateProgressbar(0, 100, 0, "Poking");
                SetLogText("#Trainers# Resonance Of Fate Poking ...");

                for (int i = 0; i < _poke.Count; i++)
                {
                    uint offsets = Functions.BytesToUInt32(Functions.HexToBytes(results[0].Offset)) + (uint)(i * 0x14);

                    SetLogText(_poke[i]);
                    _rtm.Poke(offsets, _poke[i]);
                }
                SetLogText("#Trainers# Resonance Of Fate Done... Buy the items");

                UpdateProgressbar(0, 100, 0);
            }
            catch (Exception e)
            {
                ShowMessageBox(e.Message, string.Format("Peek Poker"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Thread.CurrentThread.Abort();
            }
            #endregion
        }
        #endregion

        #region Trainer-Utility
        private void scanTrainerCodes(object sender, EventArgs e) //Opens a trainers txt file to read its codes
        {
            string _filepath2;
            SetLogText("#Trainers# Activating Trainer Scanner");
            try
            {
                OpenFileDialog Open = new OpenFileDialog();
                Open.Filter = "GAME_ID.txt|*.txt";
                Open.Title = "Open Trainer Code File";
                Open.ShowDialog();
                _filepath2 = Open.FileName;
                if (!File.Exists(_filepath2)) return;
                Application.DoEvents();
                ReadFile(_filepath2);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }
        public void ReadFile(string _filepath2)
        {
            try
            {
                TrainerTextBox.Text = File.ReadAllText(_filepath2);
                SetLogText("#Trainers# Opening Trainer, Game ID:" + _filepath2.Substring(_filepath2.Length - 12, 8)); //GAMEID Extraction from filepath

            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        //Save TrainerTextBox contents to file of users choice.
        private void createtrainerbutton_Click(object sender, EventArgs e)
        {
            SetLogText("#Trainers# Saving Trainer");
            SaveFileDialog Save = new SaveFileDialog();
            Save.Filter = "*.txt|*.txt";
            Save.Title = "Save Trainer Code File";
            Save.ShowDialog();
            if (Save.FileName != "")
            {
                System.IO.StreamWriter file = new System.IO.StreamWriter(Save.FileName);
                file.Write(TrainerTextBox.Text);
                file.Close();
                SetLogText("#Trainers# Saved Trainer to " + Save.FileName);
            }
        }

        //Appends a blank line and regains focus to trainerbox
        private void newcodebutton_Click(object sender, EventArgs e)
        {
            if (codenamebox.Text != "")
            {
                if (codeaddressbox.Text.Length == 8)
                {
                    if (codevaluebox.Text.Length >= 2)
                    {
                        TrainerTextBox.AppendText(Environment.NewLine);
                        TrainerTextBox.AppendText((Environment.NewLine) + "#" + codenamebox.Text);
                        TrainerTextBox.AppendText((Environment.NewLine) + combocodetype.SelectedIndex.ToString("X") + " " + codeaddressbox.Text + " " + codevaluebox.Text);
                        TrainerTextBox.Focus();
                    }
                    else
                    {
                        MessageBox.Show("Value must be hexadecimal!",
                            "Error", MessageBoxButtons.OK,
                            MessageBoxIcon.Exclamation,
                            MessageBoxDefaultButton.Button1);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show("The address must be 4 bytes long!",
             "Error",
             MessageBoxButtons.OK,
             MessageBoxIcon.Exclamation,
             MessageBoxDefaultButton.Button1);
                    return;
                }
            }
            else
            {
                MessageBox.Show("Please name the code!",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Exclamation,
                    MessageBoxDefaultButton.Button1);
                return;
            }
        }
        //Appends code to list, includes codename, type, address and value
        private void addcodebutton_Click(object sender, EventArgs e) //Appends code to TrainerTextBox.
        {
            if (codeaddressbox.Text.Length == 8)
            {
                if (codevaluebox.Text.Length >= 2)
                {
                    TrainerTextBox.AppendText((Environment.NewLine) + combocodetype.SelectedIndex.ToString("X") + " " + codeaddressbox.Text + " " + codevaluebox.Text);
                    TrainerTextBox.Focus();
                }
                else
                {
                    MessageBox.Show("Value must be hexadecimal!",
                        "Error", MessageBoxButtons.OK,
                        MessageBoxIcon.Exclamation,
                        MessageBoxDefaultButton.Button1);
                    return;
                }
            }
            else
            {
                MessageBox.Show("The address must be 4 bytes long!",
         "Error",
         MessageBoxButtons.OK,
         MessageBoxIcon.Exclamation,
         MessageBoxDefaultButton.Button1);
                return;
            }
        }
        #endregion
        #endregion
    }
}
