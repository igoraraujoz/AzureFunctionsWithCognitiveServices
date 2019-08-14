using System;
using System.Collections.Generic;
using System.Text;

namespace TextAnalyserFunction
{
    public class Response
    {
        public string Message { get; set; }
        public double? Score { get; set; }
        public bool Erro { get; set; }
        public string RequestMessage { get; set; }
    }
}
