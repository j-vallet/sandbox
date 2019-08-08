public IEnumerable<Contract> GetContracts(long idOrganization, IEnumerable<IContractWeekQueryable> contractWeekQueryables)
{
    Contract contractAlias = null;
    SupplierOrgResource supplierOrgResourceAlias = null;
    SupplierResource supplierResourceAlias = null;

    var results = new List<Contract>();
    int batchSize = 300;
    int currentValue = 0;

    // Restrictions
    var disjunction = Restrictions.Disjunction();

    // Regroupement par intérimaire
    foreach (var cqResources in contractWeekQueryables
        .GroupBy(cq => new 
        {
            cq.SiteId,
            cq.SupplierOrgResourceId,
            cq.SupplierResourceId,
            cq.ResourceInternalId,
            cq.ResourceExternalId,
            cq.ResourceLegalName,
            cq.ResourceGivenName
        }))
    {
        var contractConjunction = GetContractQueryableConjunction(cqResources.First(), () => supplierResourceAlias, () => supplierOrgResourceAlias);

        // Si la weekDate est renseignée on prend la période de la semaine
        // attention pour les line de type JOUR/NUI le weekdate n'est pas renseigné = null
        // on va prendre automatiquement la semaine
        // Sinon les dates début/fin
        (DateTime StartDate, DateTime EndDate) WeekPeriod(IContractWeekQueryable cwq)
            => cwq.WeekDate.HasValue
                ? cwq.WeekDate.Value.GetTruncatedWeek().StartAndEndDate()
                : cwq.StartDate.Value.FirstDayOfWeek().GetTruncatedWeek().StartAndEndDate();

        contractConjunction.Add(
            cqResources
                .Select(WeekPeriod)
                .Aggregate()
                .Select(dr => Restrictions.Where<Contract>(c =>
                    dr.EndDate >= c.StartDate
                    && dr.StartDate <= c.DisplayEndDate
                )).Disjunction()
            );

        disjunction.Add(contractConjunction);


        if (++currentValue >= batchSize)
        {
            currentValue = 0;
            var query = QueryOver(() => contractAlias)
                .Fetch(c => c.LastAssignment).Eager
                .Fetch(c => c.LastAssignment.Sector).Eager
                .Fetch(c => c.LastAssignment.OrgJobSheet).Eager
                .Fetch(c => c.LastAssignment.OrgShift).Eager
                .Fetch(c => c.Assignments).Eager
                .JoinAlias(x => x.SupplierOrgResource, () => supplierOrgResourceAlias)
                .JoinAlias(() => supplierOrgResourceAlias.SupplierResource, () => supplierResourceAlias)
                .Where(x => x.Organization.Id == idOrganization)
                .Where(disjunction);

            results.AddRange(query.TransformUsing(Transformers.DistinctRootEntity).List());
            disjunction = Restrictions.Disjunction();
        }
    }
    var query2 = QueryOver(() => contractAlias)
                .Fetch(c => c.LastAssignment).Eager
                .Fetch(c => c.LastAssignment.Sector).Eager
                .Fetch(c => c.LastAssignment.OrgJobSheet).Eager
                .Fetch(c => c.LastAssignment.OrgShift).Eager
                .Fetch(c => c.Assignments).Eager
                .JoinAlias(x => x.SupplierOrgResource, () => supplierOrgResourceAlias)
                .JoinAlias(() => supplierOrgResourceAlias.SupplierResource, () => supplierResourceAlias)
                .Where(x => x.Organization.Id == idOrganization)
                .Where(disjunction);

    results.AddRange(query2.TransformUsing(Transformers.DistinctRootEntity).List());
    return results;
}