﻿using System.Collections.Generic;
using System.Threading.Tasks;

namespace wallabag.Data.Common
{
    interface INavigable
    {
        Task OnNavigatedToAsync(object parameter, IDictionary<string, object> state);
        Task OnNavigatedFromAsync(IDictionary<string, object> pageState);
    }
}
