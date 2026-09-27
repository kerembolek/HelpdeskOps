namespace HelpdeskOps.Models.Enums
{
    public enum DeviceType { Laptop, Desktop, Printer, Switch, Router, Monitor, Other }
    public enum DeviceStatus { Active, InStorage, Faulty, Retired }
    public enum TicketCategory { Hardware, Software, Network, EmailOutlook, Printer, Other }
    public enum TicketPriority { Low, Medium, High, Critical }
    public enum TicketStatus { Open, InProgress, Resolved, Closed }
}