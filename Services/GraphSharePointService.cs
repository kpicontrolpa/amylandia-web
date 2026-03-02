using System.Net.Http.Json;
using System.Text.Json;
using AmylandiaWeb.Models;

namespace AmylandiaWeb.Services
{
    /// <summary>
    /// Repositorio genérico para SharePoint Online vía Microsoft Graph REST API.
    /// Usa un HttpClient con bearer token MSAL configurado automáticamente.
    /// </summary>
    public class GraphSharePointService
    {
        private readonly HttpClient _http;
        // ID del sitio de SharePoint (se obtiene dinámicamente en la primera llamada)
        private string? _siteId;
        private const string SiteHostAndPath = "daravina.sharepoint.com:/sites/00_Amylandia:";

        public GraphSharePointService(IHttpClientFactory httpClientFactory)
        {
            _http = httpClientFactory.CreateClient("GraphAPI");
        }

        // ─── MÉTODOS GENÉRICOS DEL REPOSITORIO ──────────────────────────────────

        /// <summary>Obtiene el ID del sitio de SharePoint y lo cachea.</summary>
        private async Task<string?> GetSiteIdAsync()
        {
            if (_siteId != null) return _siteId;
            try
            {
                var response = await _http.GetFromJsonAsync<JsonElement>($"sites/{SiteHostAndPath}");
                _siteId = response.GetProperty("id").GetString();
                return _siteId;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Recupera ítems de cualquier lista de SharePoint.
        /// </summary>
        public async Task<List<Dictionary<string, object?>>> GetListItemsAsync(string listName, string selectFields)
        {
            var siteId = await GetSiteIdAsync();
            if (siteId == null) return new();

            try
            {
                var url = $"sites/{siteId}/lists/{listName}/items?expand=fields";
                if (!string.IsNullOrWhiteSpace(selectFields))
                {
                    url += $"(select={selectFields})";
                }
                url += "&$top=5000"; // Aumentado para evitar truncar datos tempranos
                var response = await _http.GetFromJsonAsync<JsonElement>(url);
                var items = new List<Dictionary<string, object?>>();

                if (response.TryGetProperty("value", out var valueArray))
                {
                    foreach (var item in valueArray.EnumerateArray())
                    {
                        var dict = new Dictionary<string, object?>();
                        // ID del ítem de lista
                        dict["id"] = item.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                        // Campos expandidos
                        if (item.TryGetProperty("fields", out var fields))
                        {
                            foreach (var field in fields.EnumerateObject())
                            {
                                dict[field.Name] = field.Value.ValueKind switch
                                {
                                    JsonValueKind.String => field.Value.GetString(),
                                    JsonValueKind.Number => (object?)field.Value.GetDouble(),
                                    JsonValueKind.True => true,
                                    JsonValueKind.False => false,
                                    _ => field.Value.ToString()
                                };
                            }
                        }
                        items.Add(dict);
                    }
                }
                return items;
            }
            catch
            {
                return new();
            }
        }

        /// <summary>
        /// Crea un ítem en cualquier lista de SharePoint. Retorna el ID del nuevo ítem.
        /// </summary>
        public async Task<string?> CreateListItemAsync(string listName, Dictionary<string, object> fields)
        {
            var siteId = await GetSiteIdAsync();
            if (siteId == null) throw new Exception("No se pudo obtener el Site ID de SharePoint.");

            try
            {
                var payload = new { fields };
                var url = $"sites/{siteId}/lists/{listName}/items";
                var response = await _http.PostAsJsonAsync(url, payload);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<JsonElement>();
                    return result.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"SharePoint Error: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"CreateListItemAsync Error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Actualiza un ítem en cualquier lista de SharePoint usando HTTP PATCH.
        /// </summary>
        public async Task<bool> UpdateListItemAsync(string listName, int itemId, Dictionary<string, object> fields)
        {
            var siteId = await GetSiteIdAsync();
            if (siteId == null) throw new Exception("No se pudo obtener el Site ID de SharePoint.");

            try
            {
                var payload = new { fields };
                var url = $"sites/{siteId}/lists/{listName}/items/{itemId}";
                
                // Usamos HttpRequestMessage para el verbo PATCH para mayor compatibilidad
                var request = new HttpRequestMessage(new HttpMethod("PATCH"), url)
                {
                    Content = JsonContent.Create(payload)
                };
                
                var response = await _http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"SharePoint Error: {response.StatusCode} - {errorContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"UpdateListItemAsync Error: {ex.Message}");
                throw;
            }
        }

        // ─── MÉTODOS DE CONVENIENCIA (compatibilidad con Home.razor) ─────────────

        /// <summary>Obtiene el catálogo de clientes VIP desde 109_Clientes.</summary>
        public async Task<List<SharePointClient>> GetClientesAsync()
        {
            var items = await GetListItemsAsync("109_Clientes", "Title,Nombre,Reserva,Genero,Edad,Mes_Cumplea_x00f1_os");

            return items.Select(item => new SharePointClient
            {
                Id = int.TryParse(item.GetValueOrDefault("id")?.ToString(), out var id) ? id : 0,
                Title = item.GetValueOrDefault("Title")?.ToString() ?? "",
                Nombre = item.GetValueOrDefault("Nombre")?.ToString() ?? "",
                Reserva = item.GetValueOrDefault("Reserva")?.ToString() ?? "",
                Genero = item.GetValueOrDefault("Genero")?.ToString() ?? "",
                Edad = int.TryParse(item.GetValueOrDefault("Edad")?.ToString(), out var edad) ? edad : 0,
                MesCumpleanios = item.GetValueOrDefault("Mes_Cumplea_x00f1_os")?.ToString() ?? ""
            }).ToList();
        }

        public async Task<List<SharePointPaquete>> GetPaquetesAsync()
        {
            var items = await GetListItemsAsync("100_Paquete_Diario", ""); // Se extraen todos los campos
            
            return items.Select(item => new SharePointPaquete
            {
                Id = int.TryParse(item.GetValueOrDefault("id")?.ToString(), out var id) ? id : 0,
                Nombre = item.GetValueOrDefault("Paquete")?.ToString() ?? "",
                // Intentamos parsear varias posibilidades
                Precio = double.TryParse(item.GetValueOrDefault("OData__x0024__Valor")?.ToString() 
                            ?? item.GetValueOrDefault("_x0024__Valor")?.ToString()
                            ?? item.GetValueOrDefault("Valor")?.ToString()
                            ?? "0", out var precio) ? precio : 0
            }).ToList();
        }

        /// <summary>Obtiene el impuesto base (ITBMS) desde la lista 110_Impuestos.</summary>
        public async Task<SharePointImpuesto?> GetITBMSAsync()
        {
            var items = await GetListItemsAsync("110_Impuestos", ""); // Traemos todos los fields para diagnosticar
            // Obtenemos el registro más reciente (ID más alto)
            var item = items.OrderByDescending(i => int.TryParse(i.GetValueOrDefault("id")?.ToString(), out var id) ? id : 0).FirstOrDefault(); 
            
            if (item != null)
            {

                // Intentamos leer el valor de la columna Impuesto
                var impuestoStr = item.GetValueOrDefault("Impuesto")?.ToString() ?? 
                                  item.GetValueOrDefault("OData__x0025__Impuesto")?.ToString() ?? "0";
                                  
                // Si viene como "0.07" o similar, lo parseamos. Si viene como porcentaje "7.0%" lo manejamos también.
                if (double.TryParse(impuestoStr.Replace("%", ""), out var imp))
                {
                    // Si el valor viene como 0.07, al multiplicarlo por 100 lo pasamos a porcentaje para mostrarlo en UI (7%).
                    // Si ya viene como 7, lo dejamos así. Eso depende de cómo lo devuelva SharePoint (usualmente decimal).
                    // Asumiremos que viene como decimal (ej. 0.07 para 7%), así que lo multiplicamos por 100 para la UI.
                    if(imp < 1.0 && imp > 0.0)
                    {
                        imp = imp * 100;
                    }
                    
                    return new SharePointImpuesto
                    {
                        Id = int.TryParse(item.GetValueOrDefault("id")?.ToString(), out var id) ? id : 0,
                        Title = item.GetValueOrDefault("Title")?.ToString() ?? "",
                        Impuesto = imp
                    };
                }
            }
            return null;
        }

        /// <summary>Guarda un ingreso al parque en 206_Ingresos_Parque_Usuario.</summary>
        public async Task<int?> SaveIngresoAsync(SharePointIngreso ingreso)
        {
            var fields = new Dictionary<string, object>
            {
                { "Title",          ingreso.Title ?? "" },
                { "Paquete_2LookupId", ingreso.PaqueteId },
                { "Dato_Ingreso",   ingreso.Dato_Ingreso.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                { "Modo_Pago",      ingreso.Modo_Pago ?? "" },
                { "Duplica",        ingreso.Duplica },
                { "_x0025__Descuento", ingreso.Descuento ?? "" }, // Elección texto
                { "Genero",         ingreso.Genero ?? "" }, // Eleccion
                { "Edad",           ingreso.Edad ?? "" }, // Eleccion
                { "Mes",            ingreso.Mes_Cumpleanios ?? "" }, // Ajuste al nombre interno extraído "Mes"
                { "Total_Ingreso",  ingreso.Total_Ingreso } // Nueva columna numérica
            };

            if (ingreso.ClienteId.HasValue)
                fields.Add("No_ClienteLookupId", ingreso.ClienteId.Value);

            var idStr = await CreateListItemAsync("206_Ingresos_Parque_Usuario", fields);
            return int.TryParse(idStr, out var newId) ? newId : null;
        }

        /// <summary>Actualiza un ingreso existente en 206_Ingresos_Parque_Usuario.</summary>
        public async Task<bool> UpdateIngresoAsync(int id, SharePointIngreso ingreso)
        {
            var fields = new Dictionary<string, object>
            {
                { "Title",          ingreso.Title ?? "" },
                { "Paquete_2LookupId", ingreso.PaqueteId },
                { "Dato_Ingreso",   ingreso.Dato_Ingreso.ToString("yyyy-MM-ddTHH:mm:ssZ") },
                { "Modo_Pago",      ingreso.Modo_Pago ?? "" },
                { "Duplica",        ingreso.Duplica },
                { "_x0025__Descuento", ingreso.Descuento ?? "" },
                { "Genero",         ingreso.Genero ?? "" },
                { "Edad",           ingreso.Edad ?? "" },
                { "Mes",            ingreso.Mes_Cumpleanios ?? "" },
                { "Total_Ingreso",  ingreso.Total_Ingreso }
            };

            if (ingreso.ClienteId.HasValue)
                fields.Add("No_ClienteLookupId", ingreso.ClienteId.Value);

             return await UpdateListItemAsync("206_Ingresos_Parque_Usuario", id, fields);
        }

        /// <summary>Obtiene un ingreso específico por su ID.</summary>
        public async Task<SharePointIngreso?> GetIngresoByIdAsync(int id)
        {
            var siteId = await GetSiteIdAsync();
            if (siteId == null) return null;

            try
            {
                var url = $"sites/{siteId}/lists/206_Ingresos_Parque_Usuario/items/{id}?expand=fields";
                var response = await _http.GetFromJsonAsync<JsonElement>(url);
                
                if (response.TryGetProperty("fields", out var fields))
                {
                    var result = new SharePointIngreso
                    {
                        Title = fields.TryGetProperty("Title", out var title) ? title.ToString() : "",
                        ClienteId = fields.TryGetProperty("No_ClienteLookupId", out var clientId) && int.TryParse(clientId.ToString(), out var cId) ? cId : null,
                        PaqueteId = fields.TryGetProperty("Paquete_2LookupId", out var paqueteId) && int.TryParse(paqueteId.ToString(), out var pId) ? pId : 0,
                        Dato_Ingreso = fields.TryGetProperty("Dato_Ingreso", out var di) && DateTime.TryParse(di.ToString(), out var dt) ? dt.ToLocalTime() : DateTime.Now,
                        Modo_Pago = fields.TryGetProperty("Modo_Pago", out var mp) ? mp.ToString() : "",
                        Duplica = fields.TryGetProperty("Duplica", out var duplica) ? duplica.ToString() : "",
                        Descuento = fields.TryGetProperty("_x0025__Descuento", out var desc) ? desc.ToString() : "",
                        Genero = fields.TryGetProperty("Genero", out var gen) ? gen.ToString() : "",
                        Edad = fields.TryGetProperty("Edad", out var edad) ? edad.ToString() : "",
                        Mes_Cumpleanios = fields.TryGetProperty("Mes", out var mes) ? mes.ToString() : "",
                        Total_Ingreso = fields.TryGetProperty("Total_Ingreso", out var total) && double.TryParse(total.ToString().Replace(",", "."), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var t) ? t : 0
                    };
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetIngresoByIdAsync Error: {ex.Message}");
            }
            return null;
        }

        /// <summary>Calcula las sumatorias diarias desde 206_Ingresos_Parque_Usuario filtrando por fecha y metodos de pago</summary>
        public async Task<SharePointResumenDiario> GetResumenDiarioAsync(DateTime fecha)
        {
            var items = await GetListItemsAsync("206_Ingresos_Parque_Usuario", "Total_Ingreso,Modo_Pago,Dato_Ingreso");
            var resumen = new SharePointResumenDiario();

            // Formato de comparación "yyyy-MM-dd"
            var fechaStr = fecha.ToString("yyyy-MM-dd");

            foreach (var item in items)
            {
                var datoIngreso = item.GetValueOrDefault("Dato_Ingreso")?.ToString() ?? "";
                
                // Filtramos en memoria los ítems que pertenecen a la fecha provista
                if (datoIngreso.StartsWith(fechaStr))
                {
                    var modoPago = item.GetValueOrDefault("Modo_Pago")?.ToString()?.Trim() ?? "";
                    
                    // Parseo robusto del monto (manejar la posible coma continental)
                    var ingresoStr = item.GetValueOrDefault("Total_Ingreso")?.ToString() ?? "0";
                    ingresoStr = ingresoStr.Replace(",", ".");
                    
                    var totalIngreso = double.TryParse(ingresoStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ti) ? ti : 0.0;

                    if (modoPago.Equals("Efectivo", StringComparison.OrdinalIgnoreCase))
                        resumen.Efectivo += totalIngreso;
                    else if (modoPago.Equals("Yappy", StringComparison.OrdinalIgnoreCase))
                        resumen.Yappy += totalIngreso;
                    else if (modoPago.Equals("Punto de Venta", StringComparison.OrdinalIgnoreCase) || modoPago.Equals("Tarjeta", StringComparison.OrdinalIgnoreCase))
                        resumen.Tarjeta += totalIngreso;
                }
            }

            return resumen;
        }

        /// <summary>Calcula la cantidad de niños y el recaudo total agrupado por hora para el gráfico de comportamiento.</summary>
        public async Task<List<ComportamientoPorHora>> ObtenerDatosComportamiento(DateTime fecha)
        {
            var items = await GetListItemsAsync("206_Ingresos_Parque_Usuario", "Total_Ingreso,Dato_Ingreso");
            var datosPorHora = new Dictionary<string, ComportamientoPorHora>();
            
            var fechaStr = fecha.ToString("yyyy-MM-dd");

            foreach (var item in items)
            {
                var datoIngresoObj = item.GetValueOrDefault("Dato_Ingreso");
                if (datoIngresoObj == null) continue;
                
                var datoIngreso = datoIngresoObj.ToString() ?? "";
                
                if (datoIngreso.StartsWith(fechaStr))
                {
                    // Intentamos parsear la fecha para extraer la hora exacta
                    if (DateTime.TryParse(datoIngreso, out var dt))
                    {
                        // Formato militar de solo hora, ej "07"
                        var horaKey = dt.ToString("HH");
                        
                        var ingresoStr = item.GetValueOrDefault("Total_Ingreso")?.ToString() ?? "0";
                        ingresoStr = ingresoStr.Replace(",", ".");
                        var totalIngreso = double.TryParse(ingresoStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ti) ? ti : 0.0;
                        
                        if (!datosPorHora.ContainsKey(horaKey))
                        {
                            datosPorHora[horaKey] = new ComportamientoPorHora 
                            { 
                                HoraString = horaKey, 
                                CantidadNinos = 0, 
                                TotalIngresos = 0 
                            };
                        }
                        
                        // Incrementamos cantidad de tickets (niños) y sumatoria de ingresos
                        datosPorHora[horaKey].CantidadNinos++;
                        datosPorHora[horaKey].TotalIngresos += totalIngreso;
                    }
                }
            }

            // Devolver la lista ordenada cronológicamente
            return datosPorHora.Values.OrderBy(x => x.HoraString).ToList();
        }

        /// <summary>
        /// Obtiene el listado cruzado para el Monitor de Ingresos (Consulta.razor)
        /// Cruza 206_Ingresos con 100_Paquete_Diario para calcular tiempos de estadía.
        /// </summary>
        public async Task<List<IngresoMonitor>> GetMonitoreoIngresosAsync(DateTime fecha)
        {
            var ingresosTask = GetListItemsAsync("206_Ingresos_Parque_Usuario", "id,Title,Nombre_Texto,Genero,Dato_Ingreso,Duplica,Paquete_2LookupId,Modo_Pago,Total_Ingreso");
            var paquetesTask = GetPaquetesAsync(); // Extraemos todos los paquetes activos (id, nombre ej "00:15")

            await Task.WhenAll(ingresosTask, paquetesTask);

            var ingresosData = ingresosTask.Result;
            var catalogPaquetes = paquetesTask.Result;
            
            var fechaStr = fecha.ToString("yyyy-MM-dd");
            var monitores = new List<IngresoMonitor>();

            foreach (var item in ingresosData)
            {
                var datoIngresoObj = item.GetValueOrDefault("Dato_Ingreso");
                if (datoIngresoObj == null) continue;

                var datoIngresoStr = datoIngresoObj.ToString() ?? "";
                if (!datoIngresoStr.StartsWith(fechaStr)) continue; // Filtrar localmente por el día solicitado

                if (DateTime.TryParse(datoIngresoStr, out var horaEntradaOriginal))
                {
                    // Convertirla a la hora local para evitar desfases de timezone MS Graph (UTC -> Local)
                    var horaEntrada = horaEntradaOriginal.ToLocalTime(); 

                    var idLookupPaqueteStr = item.GetValueOrDefault("Paquete_2LookupId")?.ToString();
                    var idLookupPaquete = int.TryParse(idLookupPaqueteStr, out var pid) ? pid : 0;
                    
                    var paqueteObj = catalogPaquetes.FirstOrDefault(p => p.Id == idLookupPaquete);
                    var paqueteNombre = paqueteObj?.Nombre ?? "00:00"; // Ej: "00:15"

                    var duplicaStr = item.GetValueOrDefault("Duplica")?.ToString();
                    bool esDuplica = !string.IsNullOrEmpty(duplicaStr) && duplicaStr.Equals("Si", StringComparison.OrdinalIgnoreCase);

                    // --- Cálculo de Tiempo ---
                    double minutosBase = 0;
                    bool esTiempoLibre = false;
                    
                    if (paqueteNombre == "00:00")
                    {
                        esTiempoLibre = true;
                        var limiteCierre = new DateTime(horaEntrada.Year, horaEntrada.Month, horaEntrada.Day, 20, 0, 0);
                        if (horaEntrada > limiteCierre) limiteCierre = horaEntrada.AddHours(2); // Fallback: si entra después de las 20
                        minutosBase = (limiteCierre - horaEntrada).TotalMinutes;
                    }
                    else if (TimeSpan.TryParse(paqueteNombre, out var ts))
                    {
                        minutosBase = ts.TotalMinutes; // ej "00:15" -> 15 mins
                    }
                    
                    if (esDuplica && !esTiempoLibre)
                    {
                        minutosBase *= 2;
                    }

                    var horaSalida = horaEntrada.AddMinutes(minutosBase);

                    var modoPago = item.GetValueOrDefault("Modo_Pago")?.ToString() ?? "";
                    
                    var ingresoStr = item.GetValueOrDefault("Total_Ingreso")?.ToString() ?? "0";
                    ingresoStr = ingresoStr.Replace(",", ".");
                    var totalIngreso = double.TryParse(ingresoStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ti) ? ti : 0.0;

                    monitores.Add(new IngresoMonitor
                    {
                        Id = int.TryParse(item.GetValueOrDefault("id")?.ToString(), out var id) ? id : 0,
                        Nombre = item.GetValueOrDefault("Title")?.ToString() ?? "S/N",
                        Genero = item.GetValueOrDefault("Genero")?.ToString() ?? "",
                        ModoPago = modoPago,
                        TotalIngreso = totalIngreso,
                        HoraEntrada = horaEntrada,
                        PaqueteNombre = paqueteNombre,
                        EsDuplica = esDuplica,
                        EsTiempoLibre = esTiempoLibre,
                        MinutosTotales = minutosBase,
                        HoraSalida = horaSalida
                    });
                }
            }

            // Ordenamos por hora de salida para ver cuáles están por vencerse primero
            return monitores.OrderBy(m => m.HoraSalida).ToList();
        }
    }
}
