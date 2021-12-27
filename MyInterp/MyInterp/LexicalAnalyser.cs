using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LexicalAnalysis
{
    class LexicalAnalyser
    {
        //Типы символов автоматной грамматики лексического анализатора
        public enum CharType
        {
            Letter, Number, Less, Equal, Greater, Symb, NonLang, Skip, Eof
        };

        //Типы столбцов автоматной грамматики лексического анализатора
        public enum LexerType
        {
            S, F, Error, Num, Less, Equal, Greater, Id
        };

        //Типы лексем
        public enum TYPE
        {
            Id /*идентификатор*/,
            Num /*целое число*/,
            If, Else, While, Read, Write, Int, Begin, End,
            LeftRoundBracket /* ( */,
            RightRoundBracket /* ) */,
            LeftSquareBracket /* [ */,
            RightSquareBracket /* ] */,
            Less /* < */,
            Greater /* > */,
            Equal /* == */,
            Gets /* = */,
            Plus, Minus, Divide, Multiply,
            Semicolon /* ; */,
            Eof,
        };

        #region Поля данных

        private String _sourceCode; //исходный текст
        private Action<LexicalAnalyser>[] _semanticPrograms; //семантические программы
        private Dictionary<LexerType, Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>> _transferTable;//таблица переходов
        private HashSet<char> _charsToSkip; //символы, которые нужно пропускать
        private Dictionary<char, TYPE> _symbols; //символы языка
        private Dictionary<string, TYPE> _words; //ключевые слова языка
        private TYPE _type; //тип распознанной лексемы
        private string _val; //строка, накапливающая символы
        private LexerType _currentType; //текущий тип для автоматной грамматики лексического анализатора
        private char _lastReadChar; //последний прочитанный символ
        private List<Lexeme> _lexemes; //распознанные лексемы
        private int _charCount; //количество прочитанных сиволов
        private int _line; //текущая строка
        private int _col; //текущий символ строки
        private int _lastLexemeStartLine; //положение начала лексемы (строка) 
        private int _lastLexemeStartCol; //положение начала лексемы (символ) 

        #endregion

        #region Инициализация лексического анализатора

        //Описание семантических программ
        private void InitSemanticPrograms()
        {
            _semanticPrograms = new Action<LexicalAnalyser>[]
                                    {
                                        //0,Начало идентификатора
                                        l =>
                                            {
                                                l._val = l._lastReadChar.ToString();
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-1;
                                            },

                                        //1,Продолжение идентификатора
                                        l => { l._val += l._lastReadChar.ToString(); },

                                        //2,Пропуск символа
                                        l =>
                                        {
                                            if(l._lastReadChar == '\n')
                                            {
                                                l._line++;
                                                l._col = 1;
                                            }
                                            else
                                            {
                                                if (l._lastReadChar == '=')
                                                    l._val += l._lastReadChar.ToString();
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col;
                                            }
                                        },

                                        //3,Распознан символ языка
                                        l =>
                                            {
                                                l._val += l._lastReadChar.ToString();
                                                l._type = _symbols[l._lastReadChar];
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-1;
                                            },

                                        //4,Распознано ключевое слово или идентификатор
                                        l =>
                                            {
                                                l._type = _words.ContainsKey(l._val) ? _words[l._val] : TYPE.Id;
                                                l._charCount--;
                                                l._col--;
                                            },

                                        //5,Распознано число
                                        l =>
                                            {
                                                l._type = TYPE.Num;
                                                l._charCount--;
                                                l._col--;
                                            },

                                        //6,Распознан символ <
                                        l =>
                                            {
                                                l._val += l._lastReadChar.ToString();
                                                l._type = TYPE.Less;
                                                l._charCount--;
                                                l._col--;
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-1;
                                            },

                                        //7,Распознан символ =
                                        l =>
                                            {
                                                l._type = TYPE.Gets;
                                                l._charCount--;
                                                l._col--;
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-1;
                                            },

                                        //8,Распознан символ ==
                                        l =>
                                            {
                                                l._val += l._lastReadChar.ToString();
                                                l._type = TYPE.Equal;
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-2;
                                            },

                                        //9,Распознан символ >
                                        l =>
                                            {
                                                l._val += l._lastReadChar.ToString();
                                                l._type = TYPE.Greater;
                                                l._charCount--;
                                                l._col--;
                                                l._lastLexemeStartLine = l._line;
                                                l._lastLexemeStartCol = l._col-1;
                                            },

                                        //10,Символ конца файла
                                        l => { l._type = TYPE.Eof; },

                                        //11,Посторонний сивол внутри числа
                                        l =>
                                            {
                                                throw new LexerException("Посторонний символ внутри числа",_line,_col);
                                            },

                                        //12,Символ не является символом языка
                                        l =>
                                            {
                                                throw new LexerException("Символ не является символом языка",_line,_col);
                                            }

                                    };
        }

        //Описание ключевых слов языка
        private void InitWords()
        {
            _words = new Dictionary<string, TYPE>
                         {
                             {"check", TYPE.If},
                             {"notSuccess", TYPE.Else},
                             {"repeatIf", TYPE.While},
                             {"readFromFile", TYPE.Read},
                             {"writeInFile", TYPE.Write},
                             {"number", TYPE.Int}
                         };
        }

        //Описание символов языка
        private void InitSymbols()
        {
            _symbols = new Dictionary<char, TYPE>
                           {
                               {'+', TYPE.Plus},
                               {'-', TYPE.Minus},
                               {'/', TYPE.Divide},
                               {'*', TYPE.Multiply},
                               {'(', TYPE.LeftRoundBracket},
                               {')', TYPE.RightRoundBracket},
                               {'[', TYPE.LeftSquareBracket},
                               {']', TYPE.RightSquareBracket},
                               {';', TYPE.Semicolon},
                               {'{', TYPE.Begin},
                               {'}', TYPE.End}
                           };
        }

        //Описание символов, которые должен пропускать лексический анализатор
        private void InitCharsToSkip()
        {
            _charsToSkip = new HashSet<char> { ' ', '\n', '\t', '\r' };
        }

        //Описание таблицы переходов
        private void InitTransferTable()
        {
            _transferTable = new Dictionary<LexerType, Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>>();

            //LexerType.S
            _transferTable[LexerType.S] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Id,_semanticPrograms[0])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Num,_semanticPrograms[0])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Less,_semanticPrograms[2])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Equal,_semanticPrograms[2])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Greater,_semanticPrograms[2])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.S,_semanticPrograms[2])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[3])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Error,_semanticPrograms[12])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[10])},
            };

            //LexerType.Id
            _transferTable[LexerType.Id] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Id,_semanticPrograms[1])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Id,_semanticPrograms[1])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[4])},
            };

            //LexerType.Num
            _transferTable[LexerType.Num] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Error,_semanticPrograms[11])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.Num,_semanticPrograms[1])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[5])},
            };

            //LexerType.Less
            _transferTable[LexerType.Less] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[6])},
            };

            //LexerType.Greater
            _transferTable[LexerType.Greater] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[9])},
            };

            //LexerType.Equal
            _transferTable[LexerType.Equal] = new Dictionary<CharType, Tuple<LexerType, Action<LexicalAnalyser>>>
            {
                {CharType.Letter, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
                {CharType.Number, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
                {CharType.Less, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Equal, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[8])},
                {CharType.Greater, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[12])},
                {CharType.Skip, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
                {CharType.Symb, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
                {CharType.NonLang, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
                {CharType.Eof, new Tuple<LexerType, Action<LexicalAnalyser>>(LexerType.F,_semanticPrograms[7])},
            };

        }

        #endregion
        internal class Lexeme
        {
            public string Val { get; set; }
            public int Line { get; set; }
            public int CharPos { get; set; }

            public string StartsAt()
            {
                return string.Format("Line: {0}, Col: {1}", Line, CharPos);
            }

            public LexicalAnalyser.TYPE Type { get; set; }
        }
        class LexerException : Exception
        {
            public int Line;
            public int Col;

            public LexerException(string message, int Line, int Col)
                : base(message)
            {
                this.Line = Line;
                this.Col = Col;
            }

            public void PrintMessage(TextWriter outputStream)
            {
                outputStream.WriteLine("Error: {0}, at line: {1}, column: {2}", Message, Line, Col);
            }
        }
        //Получить лексемы
        public List<Lexeme> GetLexemes(string sourceCode)
        {
            _charCount = 0;
            _sourceCode = sourceCode;
            _lexemes = new List<Lexeme>();
            _line = 1;
            _col = 1;
            Lexeme lexeme;
            do
            {
                lexeme = NextToken();
            } while (lexeme.Type != TYPE.Eof);
            return _lexemes;
        }

        //Получить следущую лексему
        private Lexeme NextToken()
        {
            var lex = new Lexeme();
            _val = "";
            _currentType = LexerType.S;
            while (_currentType != LexerType.F)
            {
                NextChar();
            }
            lex.Type = _type;
            lex.Val = _val;
            lex.Line = _lastLexemeStartLine;
            lex.CharPos = _lastLexemeStartCol;
            _lexemes.Add(lex);
            return lex;
        }

        //Обработать следущий символ
        private void NextChar()
        {
            CharType charType = CharType.NonLang;
            if (_charCount >= _sourceCode.Length)
                charType = CharType.Eof;
            else
            {
                char c = _sourceCode[_charCount];
                _lastReadChar = c;
                if (char.IsLetter(c))
                    charType = CharType.Letter;
                if (char.IsNumber(c))
                    charType = CharType.Number;
                if (c == '<')
                    charType = CharType.Less;
                if (c == '=')
                    charType = CharType.Equal;
                if (c == '>')
                    charType = CharType.Greater;
                if (_symbols.ContainsKey(c))
                    charType = CharType.Symb;
                if (_charsToSkip.Contains(c))
                    charType = CharType.Skip;
            }
            _charCount++;
            _col++;
            _transferTable[_currentType][charType].Item2(this);
            _currentType = _transferTable[_currentType][charType].Item1;
        }
    }
}
