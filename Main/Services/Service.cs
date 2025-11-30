using Form;
using Npgsql;
using System.Collections.Generic;
using Data;

namespace Services;

public class Service
{
    protected ApplicationDbContext dbContext;

    public Service(ApplicationDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

}