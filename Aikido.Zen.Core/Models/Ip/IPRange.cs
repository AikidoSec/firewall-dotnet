using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Aikido.Zen.Core.Helpers;

/// <summary>
/// Represents a node in the Trie data structure used for storing IP ranges.
/// </summary>
class TrieNode
{
    public bool IsTerminal { get; set; }
    public TrieNode[] Children { get; private set; }
    public string Value { get; set; }

    public TrieNode()
    {
        IsTerminal = false;
        Children = new TrieNode[2];
        Value = null;
    }
}

/// <summary>
/// Manages a range of IP addresses using a Trie data structure for efficient storage and lookup.
/// </summary>
class IPRange
{
    private readonly TrieNode _root;
    public bool HasItems => _root.Children.Any(child => child != null);
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

    public IPRange()
    {
        _root = new TrieNode();
    }

    /// <summary>
    /// Inserts a range of IP addresses or a single IP address into the Trie.
    /// </summary>
    /// <param name="cidrOrIp">The CIDR notation of the IP range or a single IP address.</param>
    public void InsertRange(string cidrOrIp)
    {
        _lock.EnterWriteLock();
        try
        {
            var parts = cidrOrIp.Split('/');
            var ip = parts[0];
            if (!IPHelper.IsValidIp(ip))
            {
                return;
            }
            var prefixLength = parts.Length > 1 ? int.Parse(parts[1]) : 32;

            var currentNode = _root;
            foreach (var byteValue in ip.Split('.').Select(byte.Parse))
            {
                for (int bitIndex = 7; bitIndex >= 0; bitIndex--)
                {
                    if (prefixLength == 0)
                    {
                        break;
                    }

                    int bit = (byteValue >> bitIndex) & 1;
                    if (currentNode.Children[bit] == null)
                    {
                        currentNode.Children[bit] = new TrieNode();
                    }
                    currentNode = currentNode.Children[bit];
                    prefixLength--;
                }
            }

            currentNode.IsTerminal = true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Checks if a given IP address is within any of the stored IP ranges.
    /// </summary>
    /// <param name="ip">The IP address to check.</param>
    /// <returns>True if the IP is in range, otherwise false.</returns>
    public bool IsIpInRange(string ip)
    {
        _lock.EnterReadLock();
        try
        {
            // Validate the IP address before processing
            if (!IPHelper.IsValidIp(ip))
            {
                return false;
            }

            var currentNode = _root;
            foreach (var byteValue in ip.Split('.').Select(byte.Parse))
            {
                for (int bitIndex = 7; bitIndex >= 0; bitIndex--)
                {
                    int bit = (byteValue >> bitIndex) & 1;

                    if (currentNode.Children[bit] != null)
                    {
                        currentNode = currentNode.Children[bit];
                    }
                    else if (currentNode.IsTerminal)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            return currentNode.IsTerminal;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
