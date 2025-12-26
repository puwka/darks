using System;

namespace darks.Classes
{
    public class Employee
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Role { get; set; }
        public string Status { get; set; }
        public string Login { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Barcode { get; set; }

        // === ОБЯЗАТЕЛЬНО ДОБАВЬТЕ ЭТУ СТРОКУ ===
        public DateTime ExpiryDate { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? EmployeeId { get; set; }
        public string Summary => $"Заказ #{Id} ({Status})";
    }

    public class Notification
    {
        public string Title { get; set; }
        public string Message { get; set; }
    }

    public class Shift
    {
        public int Id { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double TotalHours { get; set; }
    }

    public static class CurrentSession
    {
        public static Employee User { get; set; }
    }
}