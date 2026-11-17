namespace RestaurantPOS.DAL.Enums;

public enum OrderType
{
    DineIn = 1,
    Takeaway = 2,
    Delivery = 3
}

public enum SaleStatus
{
    Completed = 1,
    Held = 2,
    Cancelled = 3,
    PartiallyReturned = 4,
    Returned = 5
}

public enum PaymentMethodType
{
    Cash = 1,
    Card = 2,
    Online = 3,
    Split = 4
}

public enum PaymentStatus
{
    Paid = 1,
    Partial = 2,
    Unpaid = 3
}

public enum StockMovementType
{
    OpeningStock = 1,
    Purchase = 2,
    PurchaseReturn = 3,
    Sale = 4,
    SalesReturn = 5,
    StockAdjustment = 6,
    Wastage = 7
}

public enum TableStatus
{
    Available = 1,
    Occupied = 2,
    Reserved = 3
}

public enum RegisterTransactionType
{
    OpeningCash = 1,
    CashSale = 2,
    CashExpense = 3,
    CashRefund = 4,
    CashPurchase = 5,
    CashDropOrPayout = 6
}
