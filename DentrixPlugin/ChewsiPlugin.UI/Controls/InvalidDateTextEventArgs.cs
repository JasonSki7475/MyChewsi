using System;

namespace ChewsiPlugin.UI.Controls
{
    internal class InvalidDateTextEventArgs : EventArgs
    {
        public InvalidDateTextEventArgs(string invalidDateString)
        {
            InvalidDateString = invalidDateString;
        }
        
        public InvalidDateTextEventArgs(string invalidDateString, string message): this(invalidDateString)
        {
            Message = message;
        }
        
        public string Message { get; private set; } = "Text does not resolve to a valid date. "
            + "Enter a date in mm/dd/yyyy format, or clear the text to represent an empty date.";
        
        public string InvalidDateString { get; } = "";
    }
}
