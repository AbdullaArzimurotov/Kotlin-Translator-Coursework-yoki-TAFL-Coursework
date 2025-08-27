using System;
using System.Linq;
using System.Windows.Forms;

namespace task
{
    public partial class Form1 : Form
    {

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBox1.Text))
            {
                MessageBox.Show("Поле не может быть пустым!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            listView1.Items.Clear();
            listBox2.Items.Clear();
            listBox3.Items.Clear();
            if (this.Controls.Find("textBoxPseudocode", true).FirstOrDefault() is TextBox tbPseudo)
            {
                tbPseudo.Clear();
            }


            LexerResult lexerResult = Lexer.Analyze(textBox1.Text);

            if (lexerResult.Tokens != null)
            {
                foreach (Token token in lexerResult.Tokens)
                {
                    var item = new ListViewItem(token.Value);
                    item.SubItems.Add(token.Type.ToString());
                    item.SubItems.Add(token.Line.ToString());
                    item.SubItems.Add(token.Column.ToString());
                    listView1.Items.Add(item);
                }
            }

            if (lexerResult.Errors != null && lexerResult.Errors.Any())
            {
                foreach (LexicalError error in lexerResult.Errors)
                {
                    listBox2.Items.Add(error.ToString());
                }
            }
            else
            {
                listBox2.Items.Add("Лексических ошибок не найдено.");
            }

            LRParser2 lRParser = new LRParser2(lexerResult.Tokens);
            ParserResult parserResult = lRParser.Parse();

            if (parserResult.Errors.Count != 0)
            {
                foreach (SyntaxError error in parserResult.Errors)
                {
                    listBox3.Items.Add(error.ToString());
                }
            }
            else
            {
                listBox3.Items.Add("Синтаксических ошибок не найдено.");
            }

            var pseudoTextBox = this.Controls.Find("textBoxPseudocode", true).FirstOrDefault() as TextBox;
            if (pseudoTextBox != null)
            {
                if (parserResult.Success)
                {
                    if (!string.IsNullOrEmpty(lRParser.GeneratedPseudocode))
                    {
                        pseudoTextBox.Text = lRParser.GeneratedPseudocode;
                    }
                    else
                    {
                        pseudoTextBox.Text = "Программа успешно распознана. Псевдокод не сгенерирован (возможно, пустая программа).";
                    }
                }
                else
                {
                    pseudoTextBox.Text = "Псевдокод не сгенерирован из-за синтаксических ошибок.";
                    if (!string.IsNullOrEmpty(lRParser.GeneratedPseudocode) && lRParser.GeneratedPseudocode.Trim().Length > 0)
                    {
                        pseudoTextBox.Text += Environment.NewLine + Environment.NewLine + "--- Частично сгенерированный псевдокод ---" + Environment.NewLine + lRParser.GeneratedPseudocode;
                    }
                }
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string fileContent = System.IO.File.ReadAllText(openFileDialog.FileName);
                    textBox1.Text = fileContent;
                }
            }
        }
    }
}