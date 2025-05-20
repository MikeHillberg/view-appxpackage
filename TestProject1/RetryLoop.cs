using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProject1;

internal class RetryLoop
{
    /// <summary>
    /// Run an async Func<bool> in a retry loop. Stops when the func returns true or on retry limit
    /// </summary>
    static async internal Task<bool> RunAsync(
        TimeSpan delay, 
        int tries,
        Func<Task<bool>> asyncFunc)
    {
        while(tries-- > 0)
        {
            if(await asyncFunc())
            {
                return true;
            }

            await Task.Delay(delay);
        }

        return false;
    }
}
