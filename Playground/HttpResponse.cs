using System.Buffers;
using System.IO.Pipelines;
using System.Text;
using URocket.Connection;
using URocket.Utils.UnmanagedMemoryManager;
using ReadResult = URocket.Utils.ReadResult;

namespace Playground;

public class HttpResponse
{
    internal static async Task HandleConnectionStreamAsync(Stream connection)
    {
        var reader = PipeReader.Create(connection);
        var writer = PipeWriter.Create(connection);
        
        while (true)
        {
            var result = await reader.ReadAsync();
            ((Connection)connection).ResetRead();
            
            var buffer = result.Buffer;
            var isCompleted = result.IsCompleted;
            if (buffer.IsEmpty && isCompleted)
                return;
            
            var sequenceReader = new SequenceReader<byte>(buffer);

            if (!sequenceReader.TryReadTo(out ReadOnlySpan<byte> msg, "\r\n\r\n"u8, true))
            {
                var data = Encoding.UTF8.GetString(buffer.ToArray());
                Console.WriteLine(data);
                continue;
            }
            reader.AdvanceTo(buffer.End);
            
            writer.Write("HTTP/1.1 200 OK\r\nContent-Length: 13\r\nContent-Type: text/plain\r\n\r\nHello, World!"u8);
            await writer.FlushAsync();
            
            //connection.Write("HTTP/1.1 200 OK\r\nContent-Length: 13\r\nContent-Type: text/plain\r\n\r\nHello, World!"u8);
            //await connection.FlushAsync();
            //((Connection)connection).ResetRead();
        }
    }
    
    internal static async Task HandleConnectionStreamAsync(Connection connection)
    {
        while (true)
        {
            ReadResult result = await connection.ReadAsync();
            if (result.IsClosed)
                break;

            // Get all ring buffers data
            UnmanagedMemoryManager[] rings = connection.GetAllSnapshotRingsAsUnmanagedMemory(result);
            ReadOnlySequence<byte> sequence = rings.ToReadOnlySequence();

            // Process received data...

            // Return rings to the kernel
            foreach (UnmanagedMemoryManager ring in rings)
                connection.ReturnRing(ring.BufferId);

            // Write the response directly into the connection slab
            ReadOnlySpan<byte> msg =
                "HTTP/1.1 200 OK\r\nContent-Length: 13\r\nContent-Type: text/plain\r\n\r\nHello, World!"u8;

            WriteDirect(connection, msg);

            // New: async flush barrier (wait until fully flushed to kernel)
            await connection.InnerFlushAsync();

            // Ready for next read cycle
            connection.ResetRead();
        }

        Console.WriteLine("HandleConnectionAsync exited.");
    }
    
    private static void WriteDirect(Connection connection, ReadOnlySpan<byte> msg)
    {
        Span<byte> dst = connection.GetSpan(msg.Length);
        msg.CopyTo(dst);
        connection.Advance(msg.Length);
    }
}