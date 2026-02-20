using System;

namespace DesignPatternChallenge
{
    // ============================
    // 1) Interface moderna (Target)
    // ============================
    public interface IPaymentProcessor
    {
        PaymentResult ProcessPayment(PaymentRequest request);
        bool RefundPayment(string transactionId, decimal amount);
        PaymentStatus CheckStatus(string transactionId);
    }

    public class PaymentRequest
    {
        public string CustomerEmail { get; set; }
        public decimal Amount { get; set; }
        public string CreditCardNumber { get; set; }
        public string Cvv { get; set; }
        public DateTime ExpirationDate { get; set; }
        public string Description { get; set; }
    }

    public class PaymentResult
    {
        public bool Success { get; set; }
        public string TransactionId { get; set; }
        public string Message { get; set; }
    }

    public enum PaymentStatus
    {
        Pending,
        Approved,
        Declined,
        Refunded
    }

    // ============================
    // 2) Sistema legado (Adaptee)
    // ============================
    public class LegacyPaymentSystem
    {
        public LegacyTransactionResponse AuthorizeTransaction(
            string cardNum,
            int cvvCode,
            int expMonth,
            int expYear,
            double amountInCents,
            string customerInfo)
        {
            Console.WriteLine($"[Sistema Legado] Autorizando transação...");
            Console.WriteLine($"Cartão: {MaskCard(cardNum)}");
            Console.WriteLine($"Valor: {(amountInCents / 100):C}");

            // Simulação (00 = aprovado)
            return new LegacyTransactionResponse
            {
                AuthCode = Guid.NewGuid().ToString()[..8].ToUpper(),
                ResponseCode = "00",
                ResponseMessage = "TRANSACTION APPROVED",
                TransactionRef = $"LEG{DateTime.Now.Ticks}"
            };
        }

        public bool ReverseTransaction(string transRef, double amountInCents)
        {
            Console.WriteLine($"[Sistema Legado] Revertendo transação {transRef}");
            Console.WriteLine($"Valor: {(amountInCents / 100):C}");
            return true;
        }

        public string QueryTransactionStatus(string transRef)
        {
            Console.WriteLine($"[Sistema Legado] Consultando transação {transRef}");
            return "APPROVED";
        }

        private static string MaskCard(string card)
        {
            if (string.IsNullOrWhiteSpace(card) || card.Length < 4) return "****";
            return new string('*', card.Length - 4) + card[^4..];
        }
    }

    public class LegacyTransactionResponse
    {
        public string AuthCode { get; set; }
        public string ResponseCode { get; set; }
        public string ResponseMessage { get; set; }
        public string TransactionRef { get; set; }
    }

    // ============================
    // 3) Adapter (faz o legado parecer moderno)
    // ============================
    // Target: IPaymentProcessor
    // Adaptee: LegacyPaymentSystem
    public class LegacyPaymentAdapter : IPaymentProcessor
    {
        private readonly LegacyPaymentSystem _legacy;

        public LegacyPaymentAdapter(LegacyPaymentSystem legacy)
        {
            _legacy = legacy;
        }

        public PaymentResult ProcessPayment(PaymentRequest request)
        {
            // Conversões necessárias
            int cvv = ParseCvv(request.Cvv);
            int expMonth = request.ExpirationDate.Month;
            int expYear = request.ExpirationDate.Year;

            // Legado usa "amountInCents" como double
            double amountInCents = (double)(request.Amount * 100);

            // Legado usa "customerInfo" como string
            string customerInfo = request.CustomerEmail;

            var legacyResponse = _legacy.AuthorizeTransaction(
                request.CreditCardNumber,
                cvv,
                expMonth,
                expYear,
                amountInCents,
                customerInfo
            );

            // Mapear resposta do legado para o modelo moderno
            bool success = legacyResponse.ResponseCode == "00";

            return new PaymentResult
            {
                Success = success,
                TransactionId = legacyResponse.TransactionRef,
                Message = success ? "Pagamento aprovado (legado)" : legacyResponse.ResponseMessage
            };
        }

        public bool RefundPayment(string transactionId, decimal amount)
        {
            // Legado trabalha com centavos
            double amountInCents = (double)(amount * 100);
            return _legacy.ReverseTransaction(transactionId, amountInCents);
        }

        public PaymentStatus CheckStatus(string transactionId)
        {
            var legacyStatus = _legacy.QueryTransactionStatus(transactionId);

            return legacyStatus switch
            {
                "APPROVED" => PaymentStatus.Approved,
                "DECLINED" => PaymentStatus.Declined,
                "REFUNDED" => PaymentStatus.Refunded,
                _ => PaymentStatus.Pending
            };
        }

        private static int ParseCvv(string cvv)
        {
            if (!int.TryParse(cvv, out var cvvInt))
                throw new ArgumentException("CVV inválido para o sistema legado (precisa ser numérico).");

            return cvvInt;
        }
    }

    // ============================
    // 4) Implementação moderna (opcional)
    // ============================
    public class ModernPaymentProcessor : IPaymentProcessor
    {
        public PaymentResult ProcessPayment(PaymentRequest request)
        {
            Console.WriteLine("[Processador Moderno] Processando pagamento...");
            return new PaymentResult
            {
                Success = true,
                TransactionId = Guid.NewGuid().ToString(),
                Message = "Pagamento aprovado"
            };
        }

        public bool RefundPayment(string transactionId, decimal amount)
        {
            Console.WriteLine($"[Processador Moderno] Reembolsando {amount:C}");
            return true;
        }

        public PaymentStatus CheckStatus(string transactionId)
        {
            return PaymentStatus.Approved;
        }
    }

    // ============================
    // 5) Cliente (Checkout não muda!)
    // ============================
    public class CheckoutService
    {
        private readonly IPaymentProcessor _paymentProcessor;

        public CheckoutService(IPaymentProcessor paymentProcessor)
        {
            _paymentProcessor = paymentProcessor;
        }

        public void CompleteOrder(string customerEmail, decimal amount, string cardNumber)
        {
            Console.WriteLine($"\n=== Finalizando Pedido ===");
            Console.WriteLine($"Cliente: {customerEmail}");
            Console.WriteLine($"Valor: {amount:C}\n");

            var request = new PaymentRequest
            {
                CustomerEmail = customerEmail,
                Amount = amount,
                CreditCardNumber = cardNumber,
                Cvv = "123",
                ExpirationDate = new DateTime(2026, 12, 31),
                Description = "Compra de produtos"
            };

            var result = _paymentProcessor.ProcessPayment(request);

            if (result.Success)
                Console.WriteLine($"✅ Pedido aprovado! ID: {result.TransactionId}");
            else
                Console.WriteLine($"❌ Pagamento recusado: {result.Message}");
        }
    }

    // ============================
    // 6) Demo
    // ============================
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Sistema de Checkout (Adapter) ===\n");

            // 1) Funciona com processador moderno
            var modern = new CheckoutService(new ModernPaymentProcessor());
            modern.CompleteOrder("cliente@email.com", 150.00m, "4111111111111111");

            Console.WriteLine("\n" + new string('-', 60) + "\n");

            // 2) Agora também funciona com o legado SEM mudar CheckoutService
            var legacySystem = new LegacyPaymentSystem();
            var legacyAdapter = new LegacyPaymentAdapter(legacySystem);

            var checkoutWithLegacy = new CheckoutService(legacyAdapter);
            checkoutWithLegacy.CompleteOrder("cliente2@email.com", 200.00m, "4111111111111111");
        }
    }
}