namespace Marius.DataContracts.SourceGenerators.DataContracts;

internal interface IGenericNameProvider
{
    int GetParameterCount();
    IList<int> GetNestedParameterCounts();
    string GetParameterName(int paramIndex);
    string GetNamespaces();
    string? GetGenericTypeName();

    bool ParametersFromBuiltInNamespaces
    {
        get;
    }
}