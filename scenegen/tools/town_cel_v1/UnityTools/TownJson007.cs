using System;
using System.Collections.Generic;
using System.Globalization;
public static class TownJson007
{
    public static object Parse(string s) { return new Reader(s).Value(); }
    class Reader
    {
        string s;int i;
        public Reader(string source){s=source;}
        void White(){while(i<s.Length&&char.IsWhiteSpace(s[i]))i++;}
        public object Value()
        {
            White();if(i>=s.Length)throw new FormatException("Unexpected JSON end");char c=s[i];
            if(c=='{'){i++;var d=new Dictionary<string,object>();White();if(s[i]=='}'){i++;return d;}while(true){White();string key=String();White();if(s[i++]!=':')throw new FormatException("Missing colon");d[key]=Value();White();c=s[i++];if(c=='}')return d;if(c!=',')throw new FormatException("Missing comma");}}
            if(c=='['){i++;var a=new List<object>();White();if(s[i]==']'){i++;return a;}while(true){a.Add(Value());White();c=s[i++];if(c==']')return a;if(c!=',')throw new FormatException("Missing array comma");}}
            if(c=='"')return String();if(c=='t'){i+=4;return true;}if(c=='f'){i+=5;return false;}if(c=='n'){i+=4;return null;}
            int start=i;while(i<s.Length&&"-+0123456789.eE".IndexOf(s[i])>=0)i++;if(start==i)throw new FormatException("Invalid JSON value at "+i);return double.Parse(s.Substring(start,i-start),CultureInfo.InvariantCulture);
        }
        string String()
        {
            if(s[i++]!='"')throw new FormatException("Expected JSON string");var b=new System.Text.StringBuilder();while(i<s.Length){char c=s[i++];if(c=='"')return b.ToString();if(c=='\\'){c=s[i++];if(c=='u'){b.Append((char)int.Parse(s.Substring(i,4),NumberStyles.HexNumber));i+=4;}else b.Append(c=='n'?'\n':c=='r'?'\r':c=='t'?'\t':c=='b'?'\b':c=='f'?'\f':c);}else b.Append(c);}throw new FormatException("Unclosed JSON string");
        }
    }
}
