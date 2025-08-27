using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace task
{
    public enum TokenType
    {
        EOF,   

        KW_FUN,
        KW_MAIN,
        KW_VAL,
        KW_VAR,
        KW_INT_TYPE,    
        KW_BYTE_TYPE,   
        KW_IF,
        KW_ELSE,

        ID,

        LIT_INT,   

        OP_ASSIGN,     
        OP_PLUS,       
        OP_MINUS,      
        OP_MUL,        
        OP_DIV,        
        OP_MOD,        
        OP_LESS,       
        OP_GREATER,    
        OP_LEQ,        
        OP_GEQ,        
        OP_EQ,         
        OP_NEQ,        
        OP_OR,         
        OP_AND,        
        OP_NOT,        

        SEP_LPAREN,    
        SEP_RPAREN,    
        SEP_LBRACE,    
        SEP_RBRACE,    
        SEP_COLON,     
        SEP_SEMICOLON  

    }

    public class Token
    {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public Token(TokenType type, string value, int line = 0, int column = 0)
        {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"Токен({Type}, '{Value}', Строка: {Line}, Поз: {Column})";
        }
    }

    public class Lexer
    {
        private enum State { S, I, D, R }

        private static bool IsLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static readonly Dictionary<string, TokenType> Keywords = new Dictionary<string, TokenType>
        {
            { "fun", TokenType.KW_FUN }, { "main", TokenType.KW_MAIN },
            { "val", TokenType.KW_VAL }, { "var", TokenType.KW_VAR },
            { "Int", TokenType.KW_INT_TYPE }, { "Byte", TokenType.KW_BYTE_TYPE },
            { "if", TokenType.KW_IF }, { "else", TokenType.KW_ELSE }
        };

        private static readonly Dictionary<char, TokenType> SingleCharTokens = new Dictionary<char, TokenType>
        {
            { '(', TokenType.SEP_LPAREN }, { ')', TokenType.SEP_RPAREN },
            { '{', TokenType.SEP_LBRACE }, { '}', TokenType.SEP_RBRACE },
            { ':', TokenType.SEP_COLON }, { ';', TokenType.SEP_SEMICOLON },
            { '+', TokenType.OP_PLUS }, { '-', TokenType.OP_MINUS },
            { '*', TokenType.OP_MUL }, { '/', TokenType.OP_DIV }, { '%', TokenType.OP_MOD }
        };

        private static char[] OperatorStartChars = new char[]
        {
            '<', '>', '=', '!', '&', '|'
        };

        private static readonly Dictionary<string, TokenType> TwoCharOperators = new Dictionary<string, TokenType>
        {
            { "<=", TokenType.OP_LEQ }, { ">=", TokenType.OP_GEQ },
            { "==", TokenType.OP_EQ }, { "!=", TokenType.OP_NEQ },
            { "&&", TokenType.OP_AND }, { "||", TokenType.OP_OR }
        };

        public static LexerResult Analyze(string inputString)
        {
            if (inputString == null)
            {
                var errorsOnly = new List<LexicalError> { new LexicalError("Входная строка не может быть null.", 0, 0) };
                return new LexerResult(new List<Token>(), errorsOnly);
            }

            List<Token> tokens = new List<Token>();
            List<LexicalError> errors = new List<LexicalError>();

            State currentState = State.S;
            StringBuilder buffer = new StringBuilder();
            int reprocessFlag = 0;

            inputString += '\n';       

            int currentLine = 1;
            int currentColumn = 0;
            int tokenStartColumn = 0;

            for (int i = 0; i < inputString.Length; i++)
            {
                char c = inputString[i];
                currentColumn++;

                if (c == '\n' && i < inputString.Length - 1)      
                {
                    currentLine++;
                    currentColumn = 0;
                }
                else if (c == '\r')      
                {
                    currentColumn = 0;
                    continue;        
                }

                for (int reprocessingLoop = 0; reprocessingLoop <= reprocessFlag; reprocessingLoop++)
                {
                    switch (currentState)
                    {
                        case State.S:
                            tokenStartColumn = currentColumn;
                            if (IsLetter(c))
                            {
                                buffer.Append(c);
                                currentState = State.I;
                            }
                            else if (char.IsDigit(c))
                            {
                                buffer.Append(c);
                                currentState = State.D;
                            }
                            else if (OperatorStartChars.Contains(c))
                            {
                                buffer.Append(c);
                                currentState = State.R;
                            }
                            else if (SingleCharTokens.ContainsKey(c))
                            {
                                tokens.Add(new Token(SingleCharTokens[c], c.ToString(), currentLine, tokenStartColumn));
                            }
                            else if (char.IsWhiteSpace(c))
                            {
                            }
                            else
                            {
                                if (i < inputString.Length - 1)       
                                {
                                    errors.Add(new LexicalError($"Неожиданный символ: '{c}'", currentLine, currentColumn));
                                }
                            }
                            break;

                        case State.I:     
                            if (char.IsLetterOrDigit(c))
                            {
                                buffer.Append(c);
                                if (buffer.Length > 128)     
                                {
                                    errors.Add(new LexicalError($"Идентификатор слишком длинный (обработан до '{buffer.ToString(0, 10)}...').", currentLine, tokenStartColumn));
                                    string val = buffer.ToString().Substring(0, 10);     
                                    tokens.Add(new Token(TokenType.ID, val, currentLine, tokenStartColumn));
                                    buffer.Clear();
                                    currentState = State.S;
                                    reprocessFlag = 1;
                                    goto EndReprocessLoop;        
                                }
                            }
                            else
                            {
                                string idValue = buffer.ToString();
                                string tokenValueForStorage = idValue;        

                                if (idValue.Length > 10)
                                {
                                    idValue = idValue.Substring(0, 10);
                                    errors.Add(new LexicalError($"Идентификатор '{tokenValueForStorage}' был усечен до '{idValue}' (10 значащих символов).", currentLine, tokenStartColumn));
                                }

                                if (Keywords.ContainsKey(idValue))
                                {
                                    tokens.Add(new Token(Keywords[idValue], idValue, currentLine, tokenStartColumn));
                                }
                                else
                                {
                                    tokens.Add(new Token(TokenType.ID, idValue, currentLine, tokenStartColumn));
                                }
                                buffer.Clear();
                                currentState = State.S;
                                reprocessFlag = 1;
                            }
                            break;

                        case State.D:   
                            if (char.IsDigit(c))
                            {
                                buffer.Append(c);
                                if (buffer.Length > 19)    
                                {
                                    errors.Add(new LexicalError($"Числовой литерал слишком длинный ('{buffer.ToString(0, 10)}...').", currentLine, tokenStartColumn));
                                    tokens.Add(new Token(TokenType.LIT_INT, buffer.ToString(0, 10), currentLine, tokenStartColumn));   
                                    buffer.Clear();
                                    currentState = State.S;
                                    reprocessFlag = 1;
                                    goto EndReprocessLoop;
                                }
                            }
                            else
                            {
                                string litValue = buffer.ToString();
                                if (long.TryParse(litValue, out long val))
                                {
                                    if (val < short.MinValue || val > short.MaxValue)
                                    {
                                        errors.Add(new LexicalError($"Целочисленный литерал '{litValue}' вне допустимого диапазона 1-2 байт ({short.MinValue}..{short.MaxValue}).", currentLine, tokenStartColumn));
                                    }
                                }
                                else          
                                {
                                    errors.Add(new LexicalError($"Некорректный формат числового литерала: '{litValue}'.", currentLine, tokenStartColumn));
                                }
                                tokens.Add(new Token(TokenType.LIT_INT, litValue, currentLine, tokenStartColumn));
                                buffer.Clear();
                                currentState = State.S;
                                reprocessFlag = 1;
                            }
                            break;

                        case State.R:     
                            string potentialTwoCharOp = buffer.ToString() + c;
                            if (TwoCharOperators.ContainsKey(potentialTwoCharOp))
                            {
                                tokens.Add(new Token(TwoCharOperators[potentialTwoCharOp], potentialTwoCharOp, currentLine, tokenStartColumn));
                                buffer.Clear();
                                currentState = State.S;
                                reprocessFlag = 0;    
                            }
                            else
                            {
                                string singleOpStr = buffer.ToString();      
                                char firstOpChar = singleOpStr[0];

                                TokenType singleOpType = TokenType.EOF;      
                                bool isValidSingleOp = true;

                                if (firstOpChar == '<') singleOpType = TokenType.OP_LESS;
                                else if (firstOpChar == '>') singleOpType = TokenType.OP_GREATER;
                                else if (firstOpChar == '=') singleOpType = TokenType.OP_ASSIGN;
                                else if (firstOpChar == '!') singleOpType = TokenType.OP_NOT;
                                else      
                                {
                                    errors.Add(new LexicalError($"Недопустимый оператор '{singleOpStr}'. Возможно, вы имели в виду '&&' или '||'?", currentLine, tokenStartColumn));
                                    isValidSingleOp = false;
                                }

                                if (isValidSingleOp)
                                {
                                    tokens.Add(new Token(singleOpType, singleOpStr, currentLine, tokenStartColumn));
                                }

                                buffer.Clear();
                                currentState = State.S;
                                reprocessFlag = 1;     
                            }
                            break;
                    }
                EndReprocessLoop:;    
                }
                reprocessFlag = 0;         
            }

            tokens.Add(new Token(TokenType.EOF, "", currentLine, 1));     

            return new LexerResult(tokens, errors);
        }
    }

    public class LexicalError
    {
        public string Message { get; }
        public int Line { get; }
        public int Column { get; }

        public LexicalError(string message, int line, int column)
        {
            Message = message;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"Ошибка (Строка {Line}, Позиция {Column}): {Message}";
        }
    }
    public class LexerResult
    {
        public List<Token> Tokens { get; }
        public List<LexicalError> Errors { get; }

        public bool HasErrors => Errors.Any();

        public LexerResult(List<Token> tokens, List<LexicalError> errors)
        {
            Tokens = tokens;      
            Errors = errors;
        }
    }
}