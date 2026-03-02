using System;
using System.Text.Json.Serialization;

namespace AmylandiaWeb.Models
{
    public class SharePointClient
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Title")] // Mapeado al Código VIP o Nombre
        public string? Title { get; set; }

        [JsonPropertyName("NombreCompleto")]
        public string? Nombre { get; set; }

        [JsonPropertyName("Reserva")] // Si aplica
        public string? Reserva { get; set; }
        
        public string? Genero { get; set; }
        public int Edad { get; set; }
        public string? MesCumpleanios { get; set; }
    }

    public class SharePointPackage
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("Title")]
        public string? Title { get; set; }

        [JsonPropertyName("Precio")]
        public double Precio { get; set; }
    }

    // Modelo estandarizado para el dropdown de Paquetes en Home.razor
    public class SharePointPaquete
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public double Precio { get; set; }
    }

    public class SharePointImpuesto
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public double Impuesto { get; set; }
    }

    public class SharePointIngreso
    {
        [JsonPropertyName("Title")] // Título obligatorio en SP
        public string? Title { get; set; }

        [JsonPropertyName("ClienteLookupId")] // Lookup ID (Obligatorio ajustado)
        public int? ClienteId { get; set; }

        [JsonPropertyName("PaqueteId")] // Cambia "PaqueteLookupId" por "PaqueteId"
        public int PaqueteId { get; set; }

        [JsonPropertyName("Dato_Ingreso")]
        public DateTime Dato_Ingreso { get; set; }

        [JsonPropertyName("Modo_Pago")]
        public string? Modo_Pago { get; set; }

        [JsonPropertyName("Duplica")]
        public string Duplica { get; set; } = string.Empty; // Elección en SP es texto

        [JsonPropertyName("OData__x0025__Descuento")] // Usualmente lleva este formato, lo mapeamos en dict
        public string? Descuento { get; set; }

        // Campos Demográficos Directos
        [JsonPropertyName("Nombre_Texto")] // Ya no existe en la lista provista, se omite de envío pero mantenemos propiedad si lo usa UI
        public string? Nombre_Texto { get; set; }

        [JsonPropertyName("Genero")]
        public string? Genero { get; set; }

        [JsonPropertyName("Edad")]
        public string? Edad { get; set; } // Elección en SP es texto

        [JsonPropertyName("Mes_Cumplea_x00f1_os")] // o Mes_x0020_Cumplea_x00f1_os
        public string? Mes_Cumpleanios { get; set; }

        [JsonPropertyName("Total_Ingreso")]
        public double Total_Ingreso { get; set; }
    }

    public class SharePointResumenDiario
    {
        public double Efectivo { get; set; }
        public double Yappy { get; set; }
        public double Tarjeta { get; set; }
        public double Total => Efectivo + Yappy + Tarjeta;
    }

    public class ComportamientoPorHora
    {
        public string HoraString { get; set; } = string.Empty;
        public int CantidadNinos { get; set; }
        public double TotalIngresos { get; set; }
    }

    public class IngresoMonitor
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Genero { get; set; } = string.Empty;
        public string ModoPago { get; set; } = string.Empty;
        public double TotalIngreso { get; set; }
        public DateTime HoraEntrada { get; set; }
        public string PaqueteNombre { get; set; } = string.Empty;
        public bool EsDuplica { get; set; }
        public DateTime HoraSalida { get; set; }
        public double MinutosTotales { get; set; }
        
        // Banderas para monitoreo especializado (ej: Tipo 00:00 Tiempo Libre)
        public bool EsTiempoLibre { get; set; }
        public bool SalioManualmente { get; set; }

        // Propiedad calculada dinámicamente según la hora actual
        public double MinutosConsumidos => SalioManualmente ? MinutosTotales : (DateTime.Now - HoraEntrada).TotalMinutes;
        public double MinutosRestantes => SalioManualmente ? 0 : MinutosTotales - MinutosConsumidos;

        public double PorcentajeAvance
        {
            get
            {
                if (SalioManualmente) return 100.0;
                if (MinutosTotales <= 0) return 0.0;
                var avance = (MinutosConsumidos / MinutosTotales) * 100;
                return Math.Clamp(avance, 0, 100);
            }
        }

        public string Estado => (SalioManualmente || DateTime.Now > HoraSalida) ? "Vencido" : "Vigente";

        public string ColorSemaforo
        {
            get
            {
                if (Estado == "Vencido") return "danger";
                if (PorcentajeAvance >= 85) return "warning";
                return "success";
            }
        }
    }
}
