import React, { useEffect, useState } from 'react';

const fetchRateLimitAccounts = async () => {
  try {
    const getAccounts = await fetch(`https://localhost:52636/api/RateLimit/GetAccounts`, {
      method: 'GET',
      headers: { 'Content-Type': 'application/json' },
    });

    if (!getAccounts.ok) throw new Error('Failed to fetch account data');

    const accounts = await getAccounts.json();

    return accounts;
  } catch (error) {
    console.error('Error fetching data:', error);
    return [];
  }
};

export default function RateLimitMonitor() {
  const [rawAccounts, setRawAccounts] = useState([]);
  const [filterNumber, setFilterNumber] = useState('');
  const [startDate, setStartDate] = useState('');
  const [endDate, setEndDate] = useState('');

  const fetchData = async () => {
    const result = await fetchRateLimitAccounts();
    setRawAccounts(result);
  };

  const filterByDateAndNumber = () => {
    const start = startDate ? new Date(startDate) : null;
    const end = endDate ? new Date(endDate) : null;

    return rawAccounts
      .map((account) => {
        const filteredNumbers = (account.numbers || []).filter((num) => {
          const matchesNumber = filterNumber.trim() === '' || num.phoneNumber.includes(filterNumber);
          const timestamp = new Date(num.lastSMSTime);
          const matchesStart = start ? timestamp >= start : true;
          const matchesEnd = end ? timestamp <= end : true;
          return matchesNumber && matchesStart && matchesEnd;
        });

        return {
          ...account,
          numbers: filteredNumbers
        };
      })
      .filter((account) => account.numbers.length > 0);
  };

  useEffect(() => {
    fetchData();
  }, []);

  const filteredAccounts = filterByDateAndNumber();

  return (
    <div className="p-6 space-y-10">
      <h1 className="text-3xl font-bold text-center">📊 Rate Limit Monitor</h1>

      <div className="grid md:grid-cols-2 gap-8">
        <div className="border p-4 rounded-xl shadow">
          <h2 className="text-xl font-semibold mb-4">Per Account</h2>
          {filteredAccounts.length === 0 && <p>No accounts available.</p>}
          {filteredAccounts.map((acct) => (
            <div key={acct.accountNumber} className="border-b py-2">
              <p>
                <strong>Account Number:</strong> {acct.accountNumber} &nbsp;|&nbsp;
                <strong>Account Limit:</strong> {acct.accountLimit}
              </p>
            </div>
          ))}
        </div>

        <div className="border p-4 rounded-xl shadow">
          <h2 className="text-xl font-semibold mb-4">Per Number</h2>

          {filteredAccounts.flatMap((acct) => acct.numbers).length === 0 && <p>No matching number data.</p>}
          {filteredAccounts.flatMap((acct) =>
            acct.numbers.map((num) => (
              <div key={num.phoneNumber + num.lastSMSTime} className="border-b py-2">
                <p>
                  <strong>Phone Number:</strong> {num.phoneNumber} &nbsp;|&nbsp;
                  <strong>Number of Checks:</strong> {num.numberOfChecks} &nbsp;|&nbsp;
                  <strong>Last SMS Time:</strong> {new Date(num.lastSMSTime).toLocaleString()}
                </p>
              </div>
            ))
          )}
          <div className="mt-6 space-y-2 border-t pt-4">
            <h3 className="text-lg font-medium">🔍 Filter Options</h3>

            <label className="block text-sm font-semibold mt-2">Filter by Number</label><br />
            <input
              type="text"
              placeholder="Optional: Filter by number"
              className="w-full px-3 py-2 border rounded"
              value={filterNumber}
              onChange={(e) => setFilterNumber(e.target.value)}
            />

            <br /><br />
            <label className="block text-sm font-semibold mt-4">Filter by Date Range</label>
            <div className="flex items-center space-x-2">
              <input
                type="datetime-local"
                className="w-full px-3 py-2 border rounded"
                value={startDate}
                onChange={(e) => setStartDate(e.target.value)}
              />
              <span>to</span>
              <input
                type="datetime-local"
                className="w-full px-3 py-2 border rounded"
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
              />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
