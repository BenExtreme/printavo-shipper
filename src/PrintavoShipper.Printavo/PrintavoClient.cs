using System.Globalization;
using System.Text.Json.Nodes;
using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.Printavo;

public class PrintavoClient : IPrintavoClient
{
    private const string OrderDetailFields = """
        id
        visualId
        visualPoNumber
        nickname
        status { id name }
        contact { id email phone customer { id companyName } }
        customerNote
        productionNote
        total
        amountOutstanding
        paidInFull
        dueAt
        customerDueAt
        deliveryMethod { name }
        contractorProfile { companyName logo phone }
        shippingAddress { companyName customerName address1 address2 city state stateIso zipCode country countryIso }
        billingAddress { companyName customerName address1 address2 city state stateIso zipCode country countryIso }
        customAddresses {
          nodes { id name companyName customerName address1 address2 city state stateIso zipCode country countryIso }
        }
        """;

    private readonly GraphQLExecutor _executor;

    public PrintavoClient(GraphQLExecutor executor)
    {
        _executor = executor;
    }

    public async Task<PrintavoUser> GetCurrentUserAsync(CancellationToken ct = default)
    {
        const string query = """
            query {
              user {
                id
                name
                email
              }
            }
            """;

        var data = await _executor.ExecuteAsync(query, ct: ct);
        var userNode = data["user"] ?? throw new PrintavoGraphQLException("Printavo did not return a user for these credentials.");

        return new PrintavoUser(
            userNode["id"]?.ToString() ?? string.Empty,
            userNode["name"]?.ToString() ?? string.Empty,
            userNode["email"]?.ToString() ?? string.Empty);
    }

    public async Task<PrintavoOrderSearchPage> SearchOrdersAsync(string? query, IReadOnlyList<string>? excludeStatusIds = null, int first = 25, string? after = null, CancellationToken ct = default)
    {
        const string gql = """
            query($query: String, $first: Int, $excludeStatusIds: [ID!], $after: String) {
              invoices(query: $query, first: $first, excludeStatusIds: $excludeStatusIds, after: $after) {
                pageInfo { hasNextPage endCursor }
                nodes {
                  id
                  visualId
                  nickname
                  status { name }
                  total
                  dueAt
                  deliveryMethod { name }
                  contact { id customer { id companyName } }
                }
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { query, first, excludeStatusIds, after }, ct);
        var connection = data["invoices"];
        var nodes = connection?["nodes"]?.AsArray() ?? [];

        return new PrintavoOrderSearchPage(
            nodes.Select(ParseOrderSummary).ToList(),
            connection?["pageInfo"]?["hasNextPage"]?.GetValue<bool>() ?? false,
            connection?["pageInfo"]?["endCursor"]?.ToString());
    }

    public async Task<PrintavoOrderDetail?> GetOrderByVisualIdAsync(string visualId, CancellationToken ct = default)
    {
        var gql = $$"""
            query($query: String, $first: Int) {
              invoices(query: $query, first: $first) {
                nodes {
                  {{OrderDetailFields}}
                }
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { query = visualId, first = 10 }, ct);
        var nodes = data["invoices"]?["nodes"]?.AsArray() ?? [];

        var match = nodes.FirstOrDefault(n => n?["visualId"]?.ToString() == visualId);
        return match is null ? null : ParseOrderDetail(match);
    }

    public async Task<PrintavoOrderDetail?> GetOrderByIdAsync(string id, CancellationToken ct = default)
    {
        var gql = $$"""
            query($id: ID!) {
              invoice(id: $id) {
                {{OrderDetailFields}}
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { id }, ct);
        var node = data["invoice"];
        return node is null ? null : ParseOrderDetail(node);
    }

    public async Task<IReadOnlyList<PrintavoCustomerSummary>> ListCustomersAsync(int first = 25, CancellationToken ct = default)
    {
        const string gql = """
            query($first: Int, $after: String) {
              customers(first: $first, after: $after) {
                nodes { id companyName }
                pageInfo { hasNextPage endCursor }
              }
            }
            """;

        // The customers query has no server-side text search and Printavo caps
        // `first` at 25, so importing a contractor requires paging through the
        // whole list. Capped at 40 pages (~1000 customers) as a runaway guard.
        var result = new List<PrintavoCustomerSummary>();
        string? cursor = null;
        for (var page = 0; page < 40; page++)
        {
            var data = await _executor.ExecuteAsync(gql, new { first, after = cursor }, ct);
            var connection = data["customers"];
            var nodes = connection?["nodes"]?.AsArray() ?? [];

            result.AddRange(nodes.Select(n =>
                new PrintavoCustomerSummary(n!["id"]!.ToString(), n["companyName"]?.ToString() ?? string.Empty)));

            var hasNextPage = connection?["pageInfo"]?["hasNextPage"]?.GetValue<bool>() ?? false;
            if (!hasNextPage)
            {
                break;
            }

            cursor = connection?["pageInfo"]?["endCursor"]?.ToString();
        }

        return result;
    }

    public async Task<PrintavoCustomerDetail?> GetCustomerAsync(string customerId, CancellationToken ct = default)
    {
        const string gql = """
            query($id: ID!) {
              customer(id: $id) {
                id
                companyName
                shippingAddress { address1 address2 city state stateIso zipCode country countryIso }
                billingAddress { address1 address2 city state stateIso zipCode country countryIso }
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { id = customerId }, ct);
        var node = data["customer"];
        if (node is null)
        {
            return null;
        }

        return new PrintavoCustomerDetail(
            node["id"]!.ToString(),
            node["companyName"]?.ToString() ?? string.Empty,
            ParseAddress(node["shippingAddress"]),
            ParseAddress(node["billingAddress"]));
    }

    public async Task<string> GetCustomerNoteAsync(string orderId, CancellationToken ct = default)
    {
        const string gql = """
            query($id: ID!) {
              invoice(id: $id) {
                customerNote
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { id = orderId }, ct);
        var invoice = data["invoice"] ?? throw new PrintavoGraphQLException("order no longer found in Printavo");
        return invoice["customerNote"]?.ToString() ?? string.Empty;
    }

    public async Task UpdateCustomerNoteAsync(string orderId, string newCustomerNoteHtml, CancellationToken ct = default)
    {
        const string gql = """
            mutation($id: ID!, $input: InvoiceInput!) {
              invoiceUpdate(id: $id, input: $input) {
                id
              }
            }
            """;

        await _executor.ExecuteAsync(gql, new { id = orderId, input = new { customerNote = newCustomerNoteHtml } }, ct);
    }

    public async Task<string> UpdateStatusAsync(string orderId, string statusId, CancellationToken ct = default)
    {
        // statusUpdate takes parentId (any status-bearing record — Task,
        // Quote, or Invoice) and returns OrderUnion (Quote|Invoice), hence
        // the inline fragment — confirmed live via schema introspection.
        const string gql = """
            mutation($parentId: ID!, $statusId: ID!) {
              statusUpdate(parentId: $parentId, statusId: $statusId) {
                ... on Invoice {
                  status { name }
                }
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { parentId = orderId, statusId }, ct);
        return data["statusUpdate"]?["status"]?["name"]?.ToString()
            ?? throw new PrintavoGraphQLException("Printavo did not return a status after updating it.");
    }

    public async Task<string> CreateTaskAsync(string name, DateTimeOffset dueAt, string? assignedToUserId, string taskableOrderId, CancellationToken ct = default)
    {
        const string gql = """
            mutation($input: TaskCreateInput!) {
              taskCreate(input: $input) {
                id
              }
            }
            """;

        // TaskCreateInput fields confirmed live via schema introspection:
        // assignedTo: IDInput, dueAt: ISO8601DateTime!, name: String!,
        // taskable: TaskableInput { id: ID!, type: TaskableType! }.
        var input = new Dictionary<string, object?>
        {
            ["name"] = name,
            ["dueAt"] = dueAt.ToString("o"),
            ["taskable"] = new { id = taskableOrderId, type = "INVOICE" }
        };
        if (!string.IsNullOrWhiteSpace(assignedToUserId))
        {
            input["assignedTo"] = new { id = assignedToUserId };
        }

        var data = await _executor.ExecuteAsync(gql, new { input }, ct);
        return data["taskCreate"]?["id"]?.ToString()
            ?? throw new PrintavoGraphQLException("Printavo did not return a task id after creating the task.");
    }

    public async Task<string> CreateInvoiceFeeAsync(string invoiceId, string description, decimal unitPrice, CancellationToken ct = default)
    {
        // feeCreate(input: FeeInput!, parentId: ID!): Fee — parentId is the
        // quote or invoice. FeeInput fields per Printavo's v2 docs: amount,
        // description, quantity, taxable, unitPrice, unitPriceAsPercentage.
        // amount (the effective total) is left for Printavo to derive from
        // unitPrice × quantity.
        const string gql = """
            mutation($parentId: ID!, $input: FeeInput!) {
              feeCreate(parentId: $parentId, input: $input) {
                id
              }
            }
            """;

        var input = new { description, unitPrice, quantity = 1, taxable = false };
        var data = await _executor.ExecuteAsync(gql, new { parentId = invoiceId, input }, ct);
        return data["feeCreate"]?["id"]?.ToString()
            ?? throw new PrintavoGraphQLException("Printavo did not return a fee id after creating the fee.");
    }

    public async Task DeleteFeeAsync(string feeId, CancellationToken ct = default)
    {
        const string gql = """
            mutation($id: ID!) {
              feeDelete(id: $id) {
                id
              }
            }
            """;

        await _executor.ExecuteAsync(gql, new { id = feeId }, ct);
    }

    /// <summary>
    /// Fetched once per app session and shared — every order screen opened
    /// used to re-page the whole user list just to fill its Assign-to picker.
    /// Everything matches users by id, so the only thing a
    /// session-long copy can get wrong is a renamed display name. Caches the
    /// in-flight Task itself so concurrent callers share one fetch; a failed
    /// or canceled fetch isn't kept, so the next call retries.
    /// </summary>
    private Task<IReadOnlyList<PrintavoUser>>? _usersCache;

    public Task<IReadOnlyList<PrintavoUser>> ListUsersAsync(CancellationToken ct = default)
    {
        if (_usersCache is { IsFaulted: false, IsCanceled: false })
        {
            return _usersCache;
        }

        _usersCache = FetchUsersAsync(ct);
        return _usersCache;
    }

    private async Task<IReadOnlyList<PrintavoUser>> FetchUsersAsync(CancellationToken ct)
    {
        // Account.users is a standard connection (per Printavo's API v2 docs),
        // paged the same way as ListCustomersAsync since `first` caps at 25.
        const string gql = """
            query($first: Int, $after: String) {
              account {
                users(first: $first, after: $after) {
                  nodes { id name email }
                  pageInfo { hasNextPage endCursor }
                }
              }
            }
            """;

        var result = new List<PrintavoUser>();
        string? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var data = await _executor.ExecuteAsync(gql, new { first = 25, after = cursor }, ct);
            var connection = data["account"]?["users"];
            var nodes = connection?["nodes"]?.AsArray() ?? [];

            result.AddRange(nodes.Select(n => new PrintavoUser(
                n!["id"]!.ToString(),
                n["name"]?.ToString() ?? string.Empty,
                n["email"]?.ToString() ?? string.Empty)));

            if (!(connection?["pageInfo"]?["hasNextPage"]?.GetValue<bool>() ?? false))
            {
                break;
            }

            cursor = connection?["pageInfo"]?["endCursor"]?.ToString();
        }

        return result;
    }

    public async Task<IReadOnlyList<PrintavoOrderStatusOption>> ListStatusesAsync(CancellationToken ct = default)
    {
        // The account's own configured status list — the same `statuses`
        // connection the hard-coded ids were originally read from. Paged like
        // the other connections since `first` caps at 25.
        const string gql = """
            query($first: Int, $after: String) {
              statuses(first: $first, after: $after) {
                nodes { id name }
                pageInfo { hasNextPage endCursor }
              }
            }
            """;

        var result = new List<PrintavoOrderStatusOption>();
        string? cursor = null;
        for (var page = 0; page < 10; page++)
        {
            var data = await _executor.ExecuteAsync(gql, new { first = 25, after = cursor }, ct);
            var connection = data["statuses"];
            var nodes = connection?["nodes"]?.AsArray() ?? [];

            result.AddRange(nodes.Select(n => new PrintavoOrderStatusOption(
                n!["id"]!.ToString(),
                n["name"]?.ToString() ?? string.Empty)));

            if (!(connection?["pageInfo"]?["hasNextPage"]?.GetValue<bool>() ?? false))
            {
                break;
            }

            cursor = connection?["pageInfo"]?["endCursor"]?.ToString();
        }

        return result;
    }

    public async Task<string?> GetOrderOwnerIdAsync(string orderId, CancellationToken ct = default)
    {
        // Deliberately its own query rather than part of OrderDetailFields —
        // if this ever fails, only the Add Task default is lost, not the
        // whole order load.
        const string gql = """
            query($id: ID!) {
              invoice(id: $id) {
                owner { id }
              }
            }
            """;

        var data = await _executor.ExecuteAsync(gql, new { id = orderId }, ct);
        return data["invoice"]?["owner"]?["id"]?.ToString();
    }

    private static PrintavoOrderSummary ParseOrderSummary(JsonNode? node)
    {
        var customer = node?["contact"]?["customer"];
        return new PrintavoOrderSummary(
            node!["id"]!.ToString(),
            node["visualId"]?.ToString() ?? string.Empty,
            node["nickname"]?.ToString(),
            node["status"]?["name"]?.ToString() ?? string.Empty,
            customer?["id"]?.ToString() ?? string.Empty,
            customer?["companyName"]?.ToString() ?? string.Empty,
            node["total"]?.GetValue<decimal>(),
            ParseDate(node["dueAt"]),
            node["deliveryMethod"]?["name"]?.ToString());
    }

    private static PrintavoOrderDetail ParseOrderDetail(JsonNode node)
    {
        var customer = node["contact"]?["customer"];
        var customAddresses = node["customAddresses"]?["nodes"]?.AsArray() ?? [];

        return new PrintavoOrderDetail(
            node["id"]!.ToString(),
            node["visualId"]?.ToString() ?? string.Empty,
            node["nickname"]?.ToString(),
            node["status"]?["name"]?.ToString() ?? string.Empty,
            node["status"]?["id"]?.ToString() ?? string.Empty,
            node["contact"]?["id"]?.ToString() ?? string.Empty,
            customer?["id"]?.ToString() ?? string.Empty,
            customer?["companyName"]?.ToString() ?? string.Empty,
            node["customerNote"]?.ToString() ?? string.Empty,
            node["productionNote"]?.ToString() ?? string.Empty,
            ParseAddress(node["shippingAddress"]),
            ParseAddress(node["billingAddress"]),
            customAddresses.Select(ParseCustomAddress).ToList(),
            node["total"]?.GetValue<decimal>(),
            ParseDate(node["dueAt"]),
            node["visualPoNumber"]?.ToString(),
            node["contact"]?["email"]?.ToString(),
            node["contact"]?["phone"]?.ToString(),
            node["deliveryMethod"]?["name"]?.ToString(),
            node["amountOutstanding"]?.GetValue<decimal>(),
            node["paidInFull"]?.GetValue<bool>() ?? false,
            DateOnly.TryParse(node["customerDueAt"]?.ToString(), CultureInfo.InvariantCulture, out var customerDue) ? customerDue : null,
            node["contractorProfile"]?["companyName"]?.ToString(),
            node["contractorProfile"]?["logo"]?.ToString(),
            node["contractorProfile"]?["phone"]?.ToString());
    }

    private static PrintavoAddress? ParseAddress(JsonNode? node)
    {
        if (node is null)
        {
            return null;
        }

        return new PrintavoAddress(
            node["companyName"]?.ToString(),
            node["customerName"]?.ToString(),
            node["address1"]?.ToString() ?? string.Empty,
            node["address2"]?.ToString(),
            node["city"]?.ToString() ?? string.Empty,
            node["state"]?.ToString() ?? string.Empty,
            node["stateIso"]?.ToString() ?? string.Empty,
            node["zipCode"]?.ToString() ?? string.Empty,
            node["country"]?.ToString() ?? string.Empty,
            node["countryIso"]?.ToString() ?? string.Empty);
    }

    private static PrintavoCustomAddress ParseCustomAddress(JsonNode? node)
    {
        return new PrintavoCustomAddress(
            node!["id"]!.ToString(),
            node["name"]?.ToString(),
            node["companyName"]?.ToString(),
            node["customerName"]?.ToString(),
            node["address1"]?.ToString() ?? string.Empty,
            node["address2"]?.ToString(),
            node["city"]?.ToString() ?? string.Empty,
            node["state"]?.ToString() ?? string.Empty,
            node["stateIso"]?.ToString() ?? string.Empty,
            node["zipCode"]?.ToString() ?? string.Empty,
            node["country"]?.ToString() ?? string.Empty,
            node["countryIso"]?.ToString() ?? string.Empty);
    }

    private static DateTimeOffset? ParseDate(JsonNode? node)
    {
        var s = node?.ToString();
        return string.IsNullOrEmpty(s) ? null : DateTimeOffset.Parse(s);
    }
}
