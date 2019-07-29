# <img height="64px" title="binsync" alt="binsync logo" src="https://user-images.githubusercontent.com/46618410/60334970-0e479c00-999d-11e9-8d35-ce9ed160b3e0.png">

Private Incremental File Storage on Usenet

#### Description

[See Gist](https://gist.github.com/retroplasma/264d9fed2350feb19f977575981bb914)

#### Important
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#### Status

We can have a "deterministic vault" in the Usenet by generating a storage code and choosing a password; keys will be generated from it. Data can be incrementally added to the vault and retrieved at a later time. It is encrypted using AES-256 (audit needed). If all metadata etc. is flushed to the Usenet we only need the code and the password to retrieve all data in the future, else we need the local cache DB. Parity is uploaded automatically (20 extra posts per 100 posts). No automatic re-uploads are implemented to deal with retention times though. The program works best with infrequent "flushes" and not too many small files because of the overhead.

The user interface is a WebDav server with a shell that can be used alongside browsing your files. The WebDav server supports reading and writing files and folders (append-only/[WORM](https://en.wikipedia.org/wiki/Write_once_read_many); no updates or deletion). Adding files has been tested with [Cyberduck](https://cyberduck.io/). Seeking into a file works, though the streaming speed hasn't been optimized yet aside from a simple look-ahead.
