using System.Collections.Generic;
using System.Linq;
using System.Text;   

namespace task
{
    public class SyntaxError
    {
        public string Message { get; }
        public int Line { get; }
        public int Column { get; }
        public int State { get; }

        public SyntaxError(string message, int line, int column, int state)
        {
            Message = message;
            Line = line;
            Column = column;
            State = state;
        }

        public override string ToString()
        {
            return $"Ошибка синтаксиса (Строка {Line}, Поз: {Column}, Состояние: {State}): {Message}";
        }
    }

    public class ParserResult
    {
        public bool Success { get; }
        public List<SyntaxError> Errors { get; }

        public bool HasErrors => Errors.Any();

        public ParserResult(bool success, List<SyntaxError> errors)
        {
            Success = success;
            Errors = errors ?? new List<SyntaxError>();
        }
    }

    public class LRParser2     
    {
        private Stack<int> stateStack;
        private Stack<Token> symbolStack;
        private List<Token> inputTokens;
        private int currentTokenIndex;
        private List<SyntaxError> errors;

        private StringBuilder pseudocodeBuilder;
        private int indentLevel;
        private int tempCounter;
        public string GeneratedPseudocode => pseudocodeBuilder?.ToString() ?? "";


        private const int NonTerminalTokenTypeBase = -1000;

        public enum NonTerminal
        {
            PROG, OPER_SPIS_OPC, OPER_SPIS, OPER, PEREM_OBJAV, TIP_PAM, TIP_DAN, INIC_OPC,
            VYR, VYR_ARIF, TERM_ARIF, MNOZH_ARIF, ATOM_ARIF, VYR_USL, OPER_BLOK,
            VYR_LOG, TERM_LOG, MNOZH_LOG, ATOM_LOG, OPER_SRAVN, OPER_PRISV, OPER_USL
        }

        public LRParser2(List<Token> tokens)
        {
            this.inputTokens = tokens;
            stateStack = new Stack<int>();
            symbolStack = new Stack<Token>();
            errors = new List<SyntaxError>();
            currentTokenIndex = 0;

            pseudocodeBuilder = new StringBuilder();
            operandStack = new Stack<string>();
        }

        private void Indent() { indentLevel++; }
        private void Dedent() { if (indentLevel > 0) indentLevel--; }
        private string GetIndentString() { return new string(' ', indentLevel * 2); }
        private void AppendIndentedLine(string line)
        {
            if (pseudocodeBuilder == null) pseudocodeBuilder = new StringBuilder();
            pseudocodeBuilder.AppendLine(GetIndentString() + line);
        }

        private Token CurrentLookahead()
        {
            if (currentTokenIndex < inputTokens.Count)
                return inputTokens[currentTokenIndex];
            var lastToken = inputTokens.LastOrDefault();
            return new Token(TokenType.EOF, "", lastToken?.Line ?? 1, (lastToken?.Column ?? 0) + (lastToken?.Value?.Length ?? 0));
        }

        private string GetTokenTypeString(Token token)   
        {
            if (token == null) return "null";
            if ((int)token.Type < NonTerminalTokenTypeBase)
            {
                NonTerminal nt = (NonTerminal)(NonTerminalTokenTypeBase - (int)token.Type);
                return $"NonTerminal <{nt.ToString()}>";
            }

            switch (token.Type)
            {
                case TokenType.EOF: return "конец файла";
                case TokenType.KW_FUN: return "ключевое слово 'fun'";
                case TokenType.KW_MAIN: return "ключевое слово 'main'";
                case TokenType.KW_VAL: return "ключевое слово 'val'";
                case TokenType.KW_VAR: return "ключевое слово 'var'";
                case TokenType.KW_INT_TYPE: return "ключевое слово 'Int'";
                case TokenType.KW_BYTE_TYPE: return "ключевое слово 'Byte'";
                case TokenType.KW_IF: return "ключевое слово 'if'";
                case TokenType.KW_ELSE: return "ключевое слово 'else'";
                case TokenType.ID: return "идентификатор";
                case TokenType.LIT_INT: return "целочисленный литерал";
                case TokenType.OP_ASSIGN: return "оператор '='";
                case TokenType.OP_PLUS: return "оператор '+'";
                case TokenType.OP_MINUS: return "оператор '-'";
                case TokenType.OP_MUL: return "оператор '*'";
                case TokenType.OP_DIV: return "оператор '/'";
                case TokenType.OP_MOD: return "оператор '%'";
                case TokenType.OP_LESS: return "оператор '<'";
                case TokenType.OP_GREATER: return "оператор '>'";
                case TokenType.OP_LEQ: return "оператор '<='";
                case TokenType.OP_GEQ: return "оператор '>='";
                case TokenType.OP_EQ: return "оператор '=='";
                case TokenType.OP_NEQ: return "оператор '!='";
                case TokenType.OP_OR: return "оператор '||'";
                case TokenType.OP_AND: return "оператор '&&'";
                case TokenType.OP_NOT: return "оператор '!'";
                case TokenType.SEP_LPAREN: return "разделитель '('";
                case TokenType.SEP_RPAREN: return "разделитель ')'";
                case TokenType.SEP_LBRACE: return "разделитель '{'";
                case TokenType.SEP_RBRACE: return "разделитель '}'";
                case TokenType.SEP_COLON: return "разделитель ':'";
                case TokenType.SEP_SEMICOLON: return "разделитель ';'";
                default: return $"неизвестный тип токена ({token.Type})";
            }
        }
        private void AddError(string baseMessage, Token offendingToken, int currentState)   
        {
            string message = baseMessage;
            int line = offendingToken?.Line ?? (inputTokens.Count > 0 ? inputTokens.Last().Line : 1);
            int col = offendingToken?.Column ?? (inputTokens.Count > 0 ? inputTokens.Last().Column : 1);

            if (offendingToken != null && offendingToken.Type != TokenType.EOF)
            {
                message += $". Обнаружено: {GetTokenTypeString(offendingToken)}";
                if (!string.IsNullOrEmpty(offendingToken.Value))
                {
                    message += $" '{offendingToken.Value}'";
                }
            }
            else
            {
                message += ". Достигнут конец ввода или неожиданный символ.";
                if (currentTokenIndex > 0 && currentTokenIndex >= inputTokens.Count)
                {
                    var lastGoodToken = inputTokens[inputTokens.Count - 1];
                    line = lastGoodToken.Line;
                    col = lastGoodToken.Column + lastGoodToken.Value.Length;
                }
                else if (currentTokenIndex < inputTokens.Count)
                {
                    var currentErrToken = inputTokens[currentTokenIndex];
                    line = currentErrToken.Line;
                    col = currentErrToken.Column;
                }
            }
            errors.Add(new SyntaxError(message, line, col, currentState));
        }

        private string GetNextTemp()
        {
            tempCounter++;
            return $"t{tempCounter}";
        }

        private Stack<string> operandStack;      

        private List<Token> Shift()   
        {
            Token lookahead = CurrentLookahead();
            symbolStack.Push(lookahead);
            currentTokenIndex++;
            return new List<Token> { lookahead };
        }

        private void Goto(int newState)   
        {
            stateStack.Push(newState);
        }

        private Token CreateNonTerminalToken(NonTerminal nt, Token basisTokenForPosition)   
        {
            return new Token(
                (TokenType)(NonTerminalTokenTypeBase - (int)nt),
                nt.ToString(),
                basisTokenForPosition?.Line ?? (symbolStack.Any() ? symbolStack.Peek().Line : 1),
                basisTokenForPosition?.Column ?? (symbolStack.Any() ? symbolStack.Peek().Column : 0)
            );
        }

        private List<Token> Reduce(int popCount, NonTerminal ntToPush)     
        {
            Token basis = null;
            List<Token> poppedSymbols = new List<Token>();

            for (int i = 0; i < popCount; i++)
            {
                if (stateStack.Count == 0) { break; }
                stateStack.Pop();
                if (symbolStack.Count == 0) { break; }
                poppedSymbols.Add(symbolStack.Pop());
            }
            poppedSymbols.Reverse();
            basis = poppedSymbols.FirstOrDefault();
            Token nonTerminalToken = CreateNonTerminalToken(ntToPush, basis);
            symbolStack.Push(nonTerminalToken);
            return poppedSymbols;
        }

        private NonTerminal? GetNonTerminalFromStackTop()   
        {
            if (symbolStack.Count > 0)
            {
                Token top = symbolStack.Peek();
                if ((int)top.Type <= NonTerminalTokenTypeBase)
                {
                    return (NonTerminal)(NonTerminalTokenTypeBase - (int)top.Type);
                }
            }
            return null;
        }

        public ParserResult Parse()
        {
            stateStack.Push(0);
            pseudocodeBuilder.Clear();
            indentLevel = 0;
            tempCounter = 0;
            operandStack.Clear();


            while (true)
            {
                if (errors.Count > 0 && errors.Count > 10)
                {
                    AddError("Слишком много ошибок, анализ остановлен.", CurrentLookahead(), stateStack.Any() ? stateStack.Peek() : -1);
                    return new ParserResult(false, errors);
                }

                int currentState = stateStack.Peek();
                Token lookahead = CurrentLookahead();
                NonTerminal? ntOnStack = GetNonTerminalFromStackTop();
                Token errorTokenForReporting = lookahead ?? (inputTokens.Any() ? inputTokens.Last() : new Token(TokenType.EOF, "", 1, 1));

                List<Token> poppedTokens;     

                switch (currentState)
                {
                    case 0:
                        if (ntOnStack == NonTerminal.PROG)
                        {
                            if (lookahead.Type == TokenType.EOF) return new ParserResult(true, errors);
                            else { AddError("Лишние символы после конструкции 'prog'", lookahead, currentState); return new ParserResult(false, errors); }
                        }
                        else if (lookahead.Type == TokenType.KW_FUN) { Shift(); Goto(1); }
                        else { AddError("Ожидался 'fun'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 1:
                        if (lookahead.Type == TokenType.KW_MAIN)
                        {
                            Shift(); Goto(2);
                            pseudocodeBuilder.AppendLine("НАЧАЛО_ПРОГРАММЫ: main()");
                        }
                        else { AddError("Ожидался 'main' после 'fun'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 2:
                        if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(3); }
                        else { AddError("Ожидался '(' после 'main'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 3:
                        if (lookahead.Type == TokenType.SEP_RPAREN) { Shift(); Goto(4); }
                        else { AddError("Ожидался ')' после '('", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 4:
                        if (lookahead.Type == TokenType.SEP_LBRACE)
                        {
                            Shift(); Goto(5);
                            AppendIndentedLine("НАЧАЛО_БЛОКА_КОДА"); Indent();
                        }
                        else { AddError("Ожидался '{' после '()'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 5:      
                        if (ntOnStack == NonTerminal.OPER_SPIS_OPC) { Goto(6); }
                        else if (ntOnStack == NonTerminal.OPER_SPIS) { Goto(8); }
                        else if (ntOnStack == NonTerminal.OPER) { Goto(11); }
                        else if (ntOnStack == NonTerminal.PEREM_OBJAV) { Goto(12); }
                        else if (ntOnStack == NonTerminal.TIP_PAM) { Goto(19); }
                        else if (ntOnStack == NonTerminal.OPER_PRISV) { Goto(131); }
                        else if (ntOnStack == NonTerminal.OPER_USL) { Goto(133); }
                        else if (ntOnStack == NonTerminal.OPER_BLOK) { Goto(134); }
                        else if (lookahead.Type == TokenType.SEP_RBRACE) { Reduce(0, NonTerminal.OPER_SPIS_OPC); }
                        else if (lookahead.Type == TokenType.KW_VAL) { Shift(); Goto(14); }
                        else if (lookahead.Type == TokenType.KW_VAR) { Shift(); Goto(15); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); Goto(81); }   
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(831); }    
                        else if (lookahead.Type == TokenType.SEP_LBRACE) { Shift(); Goto(16); }   
                        else { AddError("Ожидалось объявление переменной, присваивание, условный оператор, блок или '}'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 6:     
                        if (lookahead.Type == TokenType.SEP_RBRACE)
                        {
                            Shift(); Goto(7);
                            Dedent(); AppendIndentedLine("КОНЕЦ_БЛОКА_КОДА");
                        }
                        else { AddError("Ожидался '}' после списка операторов", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 7:        
                        Reduce(7, NonTerminal.PROG);
                        break;

                    case 8:     
                        if (ntOnStack == NonTerminal.OPER) { Goto(10); }      
                        else if (ntOnStack == NonTerminal.PEREM_OBJAV) { Goto(12); }
                        else if (ntOnStack == NonTerminal.TIP_PAM) { Goto(19); }
                        else if (ntOnStack == NonTerminal.OPER_PRISV) { Goto(131); }
                        else if (ntOnStack == NonTerminal.OPER_USL) { Goto(133); }
                        else if (ntOnStack == NonTerminal.OPER_BLOK) { Goto(134); }
                        else if (lookahead.Type == TokenType.SEP_RBRACE) { Reduce(1, NonTerminal.OPER_SPIS_OPC); }    
                        else if (lookahead.Type == TokenType.KW_VAL) { Shift(); Goto(14); }
                        else if (lookahead.Type == TokenType.KW_VAR) { Shift(); Goto(15); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); Goto(81); }
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(831); }
                        else if (lookahead.Type == TokenType.SEP_LBRACE) { Shift(); Goto(16); }
                        else { AddError("Ожидался оператор или '}'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 10:      
                        Reduce(2, NonTerminal.OPER_SPIS);
                        break;
                    case 11:     
                        Reduce(1, NonTerminal.OPER_SPIS);
                        break;

                    case 12:    
                        if (lookahead.Type == TokenType.SEP_SEMICOLON) { Shift(); Goto(13); }
                        else { AddError("Ожидался ';'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 13:        
                        Reduce(2, NonTerminal.OPER);
                        break;

                    case 131:    
                        if (lookahead.Type == TokenType.SEP_SEMICOLON) { Shift(); Goto(132); }
                        else { AddError("Ожидался ';'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 132:        
                        Reduce(2, NonTerminal.OPER);
                        break;

                    case 133:      
                        Reduce(1, NonTerminal.OPER);
                        break;
                    case 134:      
                        Reduce(1, NonTerminal.OPER);
                        break;

                    case 14:  
                        poppedTokens = Reduce(1, NonTerminal.TIP_PAM);
                        operandStack.Push(poppedTokens[0].Value);     
                        break;
                    case 15:  
                        poppedTokens = Reduce(1, NonTerminal.TIP_PAM);
                        operandStack.Push(poppedTokens[0].Value);     
                        break;

                    case 16:     
                        if (ntOnStack == NonTerminal.OPER_SPIS_OPC) { Goto(17); }
                        else if (ntOnStack == NonTerminal.OPER_SPIS) { Goto(8); }     
                        else if (ntOnStack == NonTerminal.OPER) { Goto(11); }     
                        else if (ntOnStack == NonTerminal.PEREM_OBJAV) { Goto(12); }
                        else if (ntOnStack == NonTerminal.TIP_PAM) { Goto(19); }
                        else if (ntOnStack == NonTerminal.OPER_PRISV) { Goto(131); }
                        else if (ntOnStack == NonTerminal.OPER_USL) { Goto(133); }
                        else if (ntOnStack == NonTerminal.OPER_BLOK) { Goto(134); }
                        else if (lookahead.Type == TokenType.SEP_RBRACE) { Reduce(0, NonTerminal.OPER_SPIS_OPC); }    
                        else if (lookahead.Type == TokenType.KW_VAL) { Shift(); Goto(14); }
                        else if (lookahead.Type == TokenType.KW_VAR) { Shift(); Goto(15); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); Goto(81); }
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(831); }
                        else if (lookahead.Type == TokenType.SEP_LBRACE) { Shift(); Goto(16); }   
                        else { AddError("Ожидался оператор или '}' в блоке", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 17:     
                        if (lookahead.Type == TokenType.SEP_RBRACE)
                        {
                            Shift(); Goto(18);
                            Dedent(); AppendIndentedLine("КОНЕЦ_БЛОКА_КОДА");
                        }
                        else { AddError("Ожидался '}' для завершения блока", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 18:        
                        Reduce(3, NonTerminal.OPER_BLOK);
                        break;

                    case 19:       
                        if (lookahead.Type == TokenType.ID) { Shift(); Goto(20); }
                        else { AddError("Ожидался идентификатор после 'val'/'var'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 20:       
                        if (lookahead.Type == TokenType.SEP_COLON) { Shift(); Goto(21); }
                        else { AddError("Ожидался ':' после идентификатора в объявлении", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 21:       
                        if (ntOnStack == NonTerminal.TIP_DAN) { Goto(24); }
                        else if (lookahead.Type == TokenType.KW_INT_TYPE) { Shift(); Goto(22); }
                        else if (lookahead.Type == TokenType.KW_BYTE_TYPE) { Shift(); Goto(23); }
                        else { AddError("Ожидался тип 'Int' или 'Byte'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 22:  
                        poppedTokens = Reduce(1, NonTerminal.TIP_DAN);
                        operandStack.Push(poppedTokens[0].Value);     
                        break;
                    case 23:  
                        poppedTokens = Reduce(1, NonTerminal.TIP_DAN);
                        operandStack.Push(poppedTokens[0].Value);     
                        break;

                    case 24:       
                        if (ntOnStack == NonTerminal.INIC_OPC) { Goto(25); }
                        else if (lookahead.Type == TokenType.SEP_SEMICOLON)
                        {
                            Reduce(0, NonTerminal.INIC_OPC);
                            operandStack.Push(null);       
                        }
                        else if (lookahead.Type == TokenType.OP_ASSIGN) { Shift(); Goto(26); }
                        else { AddError("Ожидался '=' для инициализации или ';'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 25:
                        poppedTokens = Reduce(5, NonTerminal.PEREM_OBJAV);

                        string exprFromInic = operandStack.Pop();        

                        string tipDanStr = operandStack.Pop();         
                        string idStr = poppedTokens[1].Value;       
                        string tipPamStr = operandStack.Pop();         

                        string varNatureResolved = (tipPamStr == "val") ? "КОНСТАНТА" : "ПЕРЕМЕННАЯ";    
                        string varNameResolved = idStr;
                        string typeNameResolved = (tipDanStr == "Int") ? "ЦЕЛЫЙ" : "БАЙТОВЫЙ";

                        AppendIndentedLine($"ОБЪЯВЛЕНИЕ_{varNatureResolved}: {varNameResolved}");
                        Indent();
                        AppendIndentedLine($"ТИП: {typeNameResolved}");

                        if (exprFromInic != null)          
                        {
                            AppendIndentedLine("НАЧАЛЬНОЕ_ЗНАЧЕНИЕ:");
                            Indent(); AppendIndentedLine(exprFromInic); Dedent();
                        }
                        Dedent();
                        break;

                    case 26:      
                        if (ntOnStack == NonTerminal.VYR) { Goto(27); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(28); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (ntOnStack == NonTerminal.VYR_USL) { Goto(49); }  
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }   
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }   
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(51); }   
                        else { AddError("Ожидалось выражение для инициализации", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 27:      
                        Reduce(2, NonTerminal.INIC_OPC);       
                        break;

                    case 28:   
                        if (lookahead.Type == TokenType.SEP_SEMICOLON || lookahead.Type == TokenType.SEP_RPAREN ||    
                            lookahead.Type == TokenType.KW_ELSE ||      
                            lookahead.Type == TokenType.OP_LESS || lookahead.Type == TokenType.OP_GREATER ||    
                            lookahead.Type == TokenType.OP_LEQ || lookahead.Type == TokenType.OP_GEQ ||
                            lookahead.Type == TokenType.OP_EQ || lookahead.Type == TokenType.OP_NEQ)
                        { Reduce(1, NonTerminal.VYR); }    
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else { AddError("Ожидался оператор '+', '-', сравнительный оператор, ';' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 29:     
                        if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(30); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался терм после '+'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 30:       
                        if (lookahead.Type == TokenType.SEP_SEMICOLON || lookahead.Type == TokenType.SEP_RPAREN ||
                           lookahead.Type == TokenType.KW_ELSE ||
                           lookahead.Type == TokenType.OP_LESS || lookahead.Type == TokenType.OP_GREATER ||
                           lookahead.Type == TokenType.OP_LEQ || lookahead.Type == TokenType.OP_GEQ ||
                           lookahead.Type == TokenType.OP_EQ || lookahead.Type == TokenType.OP_NEQ ||
                           lookahead.Type == TokenType.OP_PLUS || lookahead.Type == TokenType.OP_MINUS ||     
                           lookahead.Type == TokenType.OP_AND || lookahead.Type == TokenType.OP_OR)      
                        {
                            poppedTokens = Reduce(3, NonTerminal.VYR_ARIF);
                            string op2_desc = operandStack.Pop();
                            string op1_desc = operandStack.Pop();
                            string opSymbol = poppedTokens[1].Value;     
                            string tempRes = GetNextTemp();
                            AppendIndentedLine($"ОПЕРАЦИЯ: '{opSymbol}' (Промежуточный результат в: {tempRes})");
                            Indent();
                            AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_desc); Dedent();
                            AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_desc); Dedent();
                            Dedent();
                            operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempRes}");
                        }
                        else if (lookahead.Type == TokenType.OP_MUL) { Shift(); Goto(34); }      
                        else if (lookahead.Type == TokenType.OP_DIV) { Shift(); Goto(35); }
                        else if (lookahead.Type == TokenType.OP_MOD) { Shift(); Goto(36); }
                        else { AddError("Ожидался оператор '*', '/', '%', '+', '-', сравнительный оператор, ';' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 31:     
                        if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(32); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался терм после '-'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 32:       
                        if (lookahead.Type == TokenType.SEP_SEMICOLON || lookahead.Type == TokenType.SEP_RPAREN ||
                            lookahead.Type == TokenType.KW_ELSE ||
                            lookahead.Type == TokenType.OP_LESS || lookahead.Type == TokenType.OP_GREATER ||
                            lookahead.Type == TokenType.OP_LEQ || lookahead.Type == TokenType.OP_GEQ ||
                            lookahead.Type == TokenType.OP_EQ || lookahead.Type == TokenType.OP_NEQ ||
                            lookahead.Type == TokenType.OP_PLUS || lookahead.Type == TokenType.OP_MINUS ||
                            lookahead.Type == TokenType.OP_AND || lookahead.Type == TokenType.OP_OR)
                        {
                            poppedTokens = Reduce(3, NonTerminal.VYR_ARIF);
                            string op2_desc = operandStack.Pop();
                            string op1_desc = operandStack.Pop();
                            string opSymbol = poppedTokens[1].Value;
                            string tempRes = GetNextTemp();
                            AppendIndentedLine($"ОПЕРАЦИЯ: '{opSymbol}' (Промежуточный результат в: {tempRes})");
                            Indent();
                            AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_desc); Dedent();
                            AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_desc); Dedent();
                            Dedent();
                            operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempRes}");
                        }
                        else if (lookahead.Type == TokenType.OP_MUL) { Shift(); Goto(34); }
                        else if (lookahead.Type == TokenType.OP_DIV) { Shift(); Goto(35); }
                        else if (lookahead.Type == TokenType.OP_MOD) { Shift(); Goto(36); }
                        else { AddError("Ожидался оператор '*', '/', '%', '+', '-', сравнительный оператор, ';' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 33:     
                        if (lookahead.Type == TokenType.SEP_SEMICOLON || lookahead.Type == TokenType.SEP_RPAREN ||
                           lookahead.Type == TokenType.KW_ELSE ||
                           lookahead.Type == TokenType.OP_LESS || lookahead.Type == TokenType.OP_GREATER ||
                           lookahead.Type == TokenType.OP_LEQ || lookahead.Type == TokenType.OP_GEQ ||
                           lookahead.Type == TokenType.OP_EQ || lookahead.Type == TokenType.OP_NEQ ||
                           lookahead.Type == TokenType.OP_PLUS || lookahead.Type == TokenType.OP_MINUS ||
                           lookahead.Type == TokenType.OP_AND || lookahead.Type == TokenType.OP_OR)
                        { Reduce(1, NonTerminal.VYR_ARIF); }        
                        else if (lookahead.Type == TokenType.OP_MUL) { Shift(); Goto(34); }
                        else if (lookahead.Type == TokenType.OP_DIV) { Shift(); Goto(35); }
                        else if (lookahead.Type == TokenType.OP_MOD) { Shift(); Goto(36); }
                        else { AddError("Ожидался оператор '*', '/', '%', '+', '-', сравнительный оператор, ';' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 34:     
                        if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(37); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался множитель после '*'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 35:     
                        if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(38); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался множитель после '/'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 36:     
                        if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(39); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался множитель после '%'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 37:       
                        poppedTokens = Reduce(3, NonTerminal.TERM_ARIF);        
                        string op2_mul = operandStack.Pop(); string op1_mul = operandStack.Pop(); string opSym_mul = poppedTokens[1].Value; string temp_mul = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: '{opSym_mul}' (Промежуточный результат в: {temp_mul})"); Indent();
                        AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_mul); Dedent();
                        AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_mul); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {temp_mul}");
                        break;
                    case 38:       
                        poppedTokens = Reduce(3, NonTerminal.TERM_ARIF);   
                        string op2_div = operandStack.Pop(); string op1_div = operandStack.Pop(); string opSym_div = poppedTokens[1].Value; string temp_div = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: '{opSym_div}' (Промежуточный результат в: {temp_div})"); Indent();
                        AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_div); Dedent();
                        AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_div); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {temp_div}");
                        break;
                    case 39:       
                        poppedTokens = Reduce(3, NonTerminal.TERM_ARIF);   
                        string op2_mod = operandStack.Pop(); string op1_mod = operandStack.Pop(); string opSym_mod = poppedTokens[1].Value; string temp_mod = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: '{opSym_mod}' (Промежуточный результат в: {temp_mod})"); Indent();
                        AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_mod); Dedent();
                        AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_mod); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {temp_mod}");
                        break;

                    case 40:     
                        Reduce(1, NonTerminal.TERM_ARIF);       
                        break;
                    case 41:     
                        Reduce(1, NonTerminal.MNOZH_ARIF);       
                        break;
                    case 42:     
                        Reduce(1, NonTerminal.ATOM_ARIF);      
                        break;
                    case 43:     
                        Reduce(1, NonTerminal.ATOM_ARIF);      
                        break;
                    case 44:       
                        if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(45); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }       
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидалось арифметическое выражение внутри '('", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 45:       
                        if (lookahead.Type == TokenType.SEP_RPAREN) { Shift(); Goto(46); }
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else { AddError("Ожидался ')' или оператор", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 46:       
                        Reduce(3, NonTerminal.ATOM_ARIF);       
                        break;
                    case 47:      
                        if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(48); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }        
                        else { AddError("Ожидался множитель после унарного '-'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 48:      
                        poppedTokens = Reduce(2, NonTerminal.MNOZH_ARIF);
                        string unaryOpDesc = operandStack.Pop();
                        string tempNeg = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: 'УНАРНЫЙ_МИНУС' (Промежуточный результат в: {tempNeg})"); Indent();
                        AppendIndentedLine("ОПЕРАНД:"); Indent(); AppendIndentedLine(unaryOpDesc); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempNeg}");
                        break;

                    case 49:      
                        Reduce(1, NonTerminal.VYR);      
                        break;

                    case 51:         
                        if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(52); }
                        else { AddError("Ожидался '(' после 'if'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 52:       
                        if (ntOnStack == NonTerminal.VYR_LOG) { Goto(53); }
                        else if (ntOnStack == NonTerminal.TERM_LOG) { Goto(61); }
                        else if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(65); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(69); }       
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }    
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }    
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }    
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }      
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }   
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }   
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }         
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }   
                        else { AddError("Ожидалось логическое выражение или '(' после 'if('", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 53:       
                        if (lookahead.Type == TokenType.SEP_RPAREN)
                        {
                            Shift(); Goto(54);
                        }
                        else if (lookahead.Type == TokenType.OP_OR) { Shift(); Goto(59); }      
                        else { AddError("Ожидался ')' или '||' после логического выражения", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 54:         
                        if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(55); }  
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидалось выражение (then-ветвь) после 'if(...)'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 55:         
                        if (lookahead.Type == TokenType.KW_ELSE) { Shift(); Goto(56); }
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else { AddError("Ожидался 'else' или арифметический оператор", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 56:         
                        if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(57); }  
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидалось выражение (else-ветвь) после 'else'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 57:         
                        if (lookahead.Type == TokenType.SEP_SEMICOLON || lookahead.Type == TokenType.SEP_RPAREN)    
                        {
                            poppedTokens = Reduce(7, NonTerminal.VYR_USL);        
                            string elseExprDesc = operandStack.Pop();
                            string thenExprDesc = operandStack.Pop();
                            string condDesc = operandStack.Pop();
                            string tempIfExprRes = GetNextTemp();

                            AppendIndentedLine($"УСЛОВНОЕ_ВЫРАЖЕНИЕ (Результат в: {tempIfExprRes})"); Indent();
                            AppendIndentedLine("УСЛОВИЕ:"); Indent(); AppendIndentedLine(condDesc); Dedent();
                            AppendIndentedLine("ВЕТКА_THEN:"); Indent(); AppendIndentedLine(thenExprDesc); Dedent();
                            AppendIndentedLine("ВЕТКА_ELSE:"); Indent(); AppendIndentedLine(elseExprDesc); Dedent();
                            Dedent();
                            operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempIfExprRes}");
                        }
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else { AddError("Ожидалось завершение if-выражения или арифметический оператор", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 59:     
                        if (ntOnStack == NonTerminal.TERM_LOG) { Goto(60); }
                        else if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(65); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(69); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался логический терм после '||'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 60:       
                        if (lookahead.Type == TokenType.SEP_RPAREN || lookahead.Type == TokenType.OP_OR)       
                        {
                            poppedTokens = Reduce(3, NonTerminal.VYR_LOG);     
                            string op2_or = operandStack.Pop(); string op1_or = operandStack.Pop(); string opSym_or = poppedTokens[1].Value; string temp_or = GetNextTemp();
                            AppendIndentedLine($"ОПЕРАЦИЯ: '{opSym_or}' (Промежуточный результат в: {temp_or})"); Indent();
                            AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_or); Dedent();
                            AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_or); Dedent(); Dedent();
                            operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {temp_or}");
                        }
                        else if (lookahead.Type == TokenType.OP_AND) { Shift(); Goto(63); }      
                        else { AddError("Ожидался '&&', '||' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 61:     
                        if (lookahead.Type == TokenType.SEP_RPAREN || lookahead.Type == TokenType.OP_OR)
                        { Reduce(1, NonTerminal.VYR_LOG); }       
                        else if (lookahead.Type == TokenType.OP_AND) { Shift(); Goto(63); }
                        else { AddError("Ожидался '&&', '||' или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 63:     
                        if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(64); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(69); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался логический множитель после '&&'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 64:       
                        poppedTokens = Reduce(3, NonTerminal.TERM_LOG);     
                        string op2_and = operandStack.Pop(); string op1_and = operandStack.Pop(); string opSym_and = poppedTokens[1].Value; string temp_and = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: '{opSym_and}' (Промежуточный результат в: {temp_and})"); Indent();
                        AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_and); Dedent();
                        AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_and); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {temp_and}");
                        break;
                    case 65:     
                        Reduce(1, NonTerminal.TERM_LOG);      
                        break;

                    case 66:      
                        if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(67); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(69); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }     
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидался логический множитель после '!'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 67:      
                        poppedTokens = Reduce(2, NonTerminal.MNOZH_LOG);
                        string not_op_desc = operandStack.Pop();
                        string tempNot = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ: 'ЛОГИЧЕСКОЕ_НЕ' (Промежуточный результат в: {tempNot})"); Indent();
                        AppendIndentedLine("ОПЕРАНД:"); Indent(); AppendIndentedLine(not_op_desc); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempNot}");
                        break;
                    case 68:     
                        Reduce(1, NonTerminal.MNOZH_LOG);      
                        break;

                    case 69:       
                        if (ntOnStack == NonTerminal.OPER_SRAVN) { Goto(76); }
                        else if (lookahead.Type == TokenType.OP_LESS) { Shift(); Goto(70); }
                        else if (lookahead.Type == TokenType.OP_GREATER) { Shift(); Goto(71); }
                        else if (lookahead.Type == TokenType.OP_LEQ) { Shift(); Goto(72); }
                        else if (lookahead.Type == TokenType.OP_GEQ) { Shift(); Goto(73); }
                        else if (lookahead.Type == TokenType.OP_EQ) { Shift(); Goto(74); }
                        else if (lookahead.Type == TokenType.OP_NEQ) { Shift(); Goto(75); }
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else { AddError("Ожидался оператор сравнения или арифметический оператор", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 70: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push("<"); break;
                    case 71: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push(">"); break;
                    case 72: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push("<="); break;
                    case 73: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push(">="); break;
                    case 74: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push("=="); break;
                    case 75: Reduce(1, NonTerminal.OPER_SRAVN); operandStack.Push("!="); break;

                    case 76:       
                        if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(77); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидалось арифметическое выражение после оператора сравнения", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 77:       
                        poppedTokens = Reduce(3, NonTerminal.ATOM_LOG);
                        string op2_comp_desc = operandStack.Pop();   
                        string compOpStr = operandStack.Pop();         
                        string op1_comp_desc = operandStack.Pop();   
                        string tempCompRes = GetNextTemp();
                        AppendIndentedLine($"ОПЕРАЦИЯ_СРАВНЕНИЯ: '{compOpStr}' (Промежуточный результат в: {tempCompRes})"); Indent();
                        AppendIndentedLine("ОПЕРАНД1:"); Indent(); AppendIndentedLine(op1_comp_desc); Dedent();
                        AppendIndentedLine("ОПЕРАНД2:"); Indent(); AppendIndentedLine(op2_comp_desc); Dedent(); Dedent();
                        operandStack.Push($"ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: {tempCompRes}");
                        break;

                    case 78:                
                        if (ntOnStack == NonTerminal.VYR_LOG) { Goto(79); }
                        else if (ntOnStack == NonTerminal.TERM_LOG) { Goto(61); }
                        else if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(65); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(781); }        
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }       
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }     
                        else { AddError("Ожидалось логическое или арифметическое выражение внутри '('", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 781:             
                        if (ntOnStack == NonTerminal.OPER_SRAVN) { Goto(76); }    
                        else if (lookahead.Type == TokenType.OP_PLUS) { Shift(); Goto(29); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(31); }
                        else if (lookahead.Type == TokenType.OP_LESS) { Shift(); Goto(70); }
                        else if (lookahead.Type == TokenType.OP_GREATER) { Shift(); Goto(71); }
                        else if (lookahead.Type == TokenType.OP_LEQ) { Shift(); Goto(72); }
                        else if (lookahead.Type == TokenType.OP_GEQ) { Shift(); Goto(73); }
                        else if (lookahead.Type == TokenType.OP_EQ) { Shift(); Goto(74); }
                        else if (lookahead.Type == TokenType.OP_NEQ) { Shift(); Goto(75); }
                        else if (lookahead.Type == TokenType.SEP_RPAREN)
                        {         
                            AddError("Ожидалось логическое выражение (например, сравнение) в ( ), а не арифметическое.", errorTokenForReporting, currentState);
                            return new ParserResult(false, errors);
                        }
                        else { AddError("Ожидался оператор сравнения, арифметический оператор или ')'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;

                    case 79:       
                        if (lookahead.Type == TokenType.SEP_RPAREN) { Shift(); Goto(80); }
                        else if (lookahead.Type == TokenType.OP_OR) { Shift(); Goto(59); }   
                        else { AddError("Ожидался ')' или '||'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 80:       
                        Reduce(3, NonTerminal.ATOM_LOG);      
                        break;

                    case 81:     
                        if (lookahead.Type == TokenType.OP_ASSIGN)
                        {
                            operandStack.Push($"ИДЕНТИФИКАТОР: {symbolStack.Peek().Value}");
                            Shift(); Goto(82);
                        }
                        else { AddError("Ожидался '=' для присваивания", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 82:     
                        if (ntOnStack == NonTerminal.VYR) { Goto(83); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(28); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (ntOnStack == NonTerminal.VYR_USL) { Goto(49); }  
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(44); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(51); }  
                        else { AddError("Ожидалось выражение после '='", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 83:       
                        poppedTokens = Reduce(3, NonTerminal.OPER_PRISV);
                        string assign_expr_desc = operandStack.Pop();   
                        string assign_target_id_desc = operandStack.Pop();        
                        string targetVarName = assign_target_id_desc.StartsWith("ИДЕНТИФИКАТОР: ") ?
                                               assign_target_id_desc.Substring("ИДЕНТИФИКАТОР: ".Length) :
                                               poppedTokens[0].Value;    

                        AppendIndentedLine($"ПРИСВАИВАНИЕ: {targetVarName}"); Indent();
                        AppendIndentedLine("ЗНАЧЕНИЕ:"); Indent(); AppendIndentedLine(assign_expr_desc); Dedent(); Dedent();
                        break;

                    case 831:      
                        if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(84); }
                        else { AddError("Ожидался '(' после 'if' в условном операторе", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 84:        
                        if (ntOnStack == NonTerminal.VYR_LOG) { Goto(85); }
                        else if (ntOnStack == NonTerminal.TERM_LOG) { Goto(61); }
                        else if (ntOnStack == NonTerminal.MNOZH_LOG) { Goto(65); }
                        else if (ntOnStack == NonTerminal.ATOM_LOG) { Goto(68); }
                        else if (ntOnStack == NonTerminal.VYR_ARIF) { Goto(69); }
                        else if (ntOnStack == NonTerminal.TERM_ARIF) { Goto(33); }
                        else if (ntOnStack == NonTerminal.MNOZH_ARIF) { Goto(40); }
                        else if (ntOnStack == NonTerminal.ATOM_ARIF) { Goto(41); }
                        else if (lookahead.Type == TokenType.OP_NOT) { Shift(); Goto(66); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); operandStack.Push($"ИДЕНТИФИКАТОР: {lookahead.Value}"); Goto(42); }
                        else if (lookahead.Type == TokenType.LIT_INT) { Shift(); operandStack.Push($"ЦЕЛЫЙ_ЛИТЕРАЛ: {lookahead.Value}"); Goto(43); }
                        else if (lookahead.Type == TokenType.SEP_LPAREN) { Shift(); Goto(78); }
                        else if (lookahead.Type == TokenType.OP_MINUS) { Shift(); Goto(47); }
                        else { AddError("Ожидалось логическое выражение или '(' после 'if('", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 85:        
                        if (lookahead.Type == TokenType.SEP_RPAREN)
                        {
                            Shift(); Goto(86);
                            string condIfStmtDesc = operandStack.Pop();
                            AppendIndentedLine($"ЕСЛИ ({condIfStmtDesc.Replace("ПРОМЕЖУТОЧНЫЙ_РЕЗУЛЬТАТ: ", "").Replace("ИДЕНТИФИКАТОР: ", "").Replace("ЦЕЛЫЙ_ЛИТЕРАЛ: ", "")}) ТО");
                            Indent();
                        }
                        else if (lookahead.Type == TokenType.OP_OR) { Shift(); Goto(59); }
                        else { AddError("Ожидался ')' или '||' после условия if", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 86:           
                        if (ntOnStack == NonTerminal.OPER) { Goto(87); }
                        else if (ntOnStack == NonTerminal.PEREM_OBJAV) { Goto(12); }
                        else if (ntOnStack == NonTerminal.TIP_PAM) { Goto(19); }    
                        else if (ntOnStack == NonTerminal.OPER_PRISV) { Goto(131); }
                        else if (ntOnStack == NonTerminal.OPER_USL) { Goto(133); }    
                        else if (ntOnStack == NonTerminal.OPER_BLOK) { Goto(134); }
                        else if (lookahead.Type == TokenType.KW_VAL) { Shift(); Goto(14); }    
                        else if (lookahead.Type == TokenType.KW_VAR) { Shift(); Goto(15); }    
                        else if (lookahead.Type == TokenType.ID) { Shift(); Goto(81); }    
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(831); }     
                        else if (lookahead.Type == TokenType.SEP_LBRACE) { Shift(); Goto(16); }
                        else { AddError("Ожидался оператор (then-ветвь) после 'if(...)'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 87:         
                        if (lookahead.Type == TokenType.KW_ELSE)
                        {
                            Shift(); Goto(93);
                            Dedent();    
                            AppendIndentedLine("ИНАЧЕ"); Indent();    
                        }
                        else if (lookahead.Type == TokenType.SEP_RBRACE ||     
                                 lookahead.Type == TokenType.KW_VAL ||   
                                 lookahead.Type == TokenType.KW_VAR ||
                                 lookahead.Type == TokenType.ID ||
                                 lookahead.Type == TokenType.KW_IF ||
                                 lookahead.Type == TokenType.SEP_LBRACE ||
                                 lookahead.Type == TokenType.SEP_SEMICOLON ||           
                                 lookahead.Type == TokenType.EOF)
                        {
                            Reduce(5, NonTerminal.OPER_USL);      
                            Dedent();    
                            AppendIndentedLine("КОНЕЦ_ЕСЛИ");
                        }
                        else { AddError("Ожидался 'else' или конец условного оператора", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 93:            
                        if (ntOnStack == NonTerminal.OPER) { Goto(94); }
                        else if (ntOnStack == NonTerminal.PEREM_OBJAV) { Goto(12); }
                        else if (ntOnStack == NonTerminal.TIP_PAM) { Goto(19); }
                        else if (ntOnStack == NonTerminal.OPER_PRISV) { Goto(131); }
                        else if (ntOnStack == NonTerminal.OPER_USL) { Goto(133); }
                        else if (ntOnStack == NonTerminal.OPER_BLOK) { Goto(134); }
                        else if (lookahead.Type == TokenType.KW_VAL) { Shift(); Goto(14); }
                        else if (lookahead.Type == TokenType.KW_VAR) { Shift(); Goto(15); }
                        else if (lookahead.Type == TokenType.ID) { Shift(); Goto(81); }
                        else if (lookahead.Type == TokenType.KW_IF) { Shift(); Goto(831); }
                        else if (lookahead.Type == TokenType.SEP_LBRACE) { Shift(); Goto(16); }
                        else { AddError("Ожидался оператор (else-ветвь) после 'else'", errorTokenForReporting, currentState); return new ParserResult(false, errors); }
                        break;
                    case 94:         
                        Reduce(7, NonTerminal.OPER_USL);        
                        Dedent();    
                        AppendIndentedLine("КОНЕЦ_ЕСЛИ");
                        break;

                    default:
                        AddError($"Неизвестное или необработанное состояние парсера: {currentState}", errorTokenForReporting, currentState);
                        return new ParserResult(false, errors);
                }
            }
        }
    }
}