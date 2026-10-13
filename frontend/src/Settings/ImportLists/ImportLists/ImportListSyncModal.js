import PropTypes from 'prop-types';
import React, { Component } from 'react';
import Button from 'Components/Link/Button';
import LoadingIndicator from 'Components/Loading/LoadingIndicator';
import Modal from 'Components/Modal/Modal';
import ModalBody from 'Components/Modal/ModalBody';
import ModalContent from 'Components/Modal/ModalContent';
import ModalFooter from 'Components/Modal/ModalFooter';
import ModalHeader from 'Components/Modal/ModalHeader';
import { kinds, sizes } from 'Helpers/Props';
import createAjaxRequest from 'Utilities/createAjaxRequest';
import translate from 'Utilities/String/translate';
import styles from './ImportListSyncModal.css';

function getErrorMessage(xhr) {
  return xhr?.responseJSON?.message || xhr?.responseJSON?.detail || xhr?.statusText || translate('RequestFailed');
}

function formatDuration(durationMilliseconds) {
  const seconds = Math.max(0, Number(durationMilliseconds || 0)) / 1000;
  return `${seconds.toFixed(1)}s`;
}

class ImportListSyncModal extends Component {
  constructor(props, context) {
    super(props, context);

    this.state = {
      isLoadingHistory: false,
      historyError: null,
      history: [],
      isPreviewing: false,
      previewError: null,
      preview: null
    };
  }

  componentDidMount() {
    if (this.props.isOpen) {
      this.fetchHistory();
    }
  }

  componentDidUpdate(prevProps) {
    if (!prevProps.isOpen && this.props.isOpen) {
      this.setState({ preview: null, previewError: null });
      this.fetchHistory();
    }

    if (prevProps.isOpen && !this.props.isOpen) {
      this._historyRequest?.abortRequest();
      this._previewRequest?.abortRequest();
    }
  }

  componentWillUnmount() {
    this._historyRequest?.abortRequest();
    this._previewRequest?.abortRequest();
  }

  fetchHistory = () => {
    const { id } = this.props;

    this.setState({ isLoadingHistory: true, historyError: null });

    this._historyRequest = createAjaxRequest({
      url: `/importlist/${id}/history`,
      dataType: 'json'
    });

    this._historyRequest.request.done((history) => {
      this.setState({ history: history || [], isLoadingHistory: false });
    });

    this._historyRequest.request.fail((xhr) => {
      if (xhr?.aborted) {
        return;
      }

      this.setState({ historyError: getErrorMessage(xhr), isLoadingHistory: false });
    });
  };

  onPreviewPress = () => {
    const { id } = this.props;

    this.setState({ isPreviewing: true, previewError: null, preview: null });

    this._previewRequest = createAjaxRequest({
      url: `/importlist/${id}/preview`,
      method: 'POST',
      data: '{}',
      dataType: 'json'
    });

    this._previewRequest.request.done((preview) => {
      this.setState({ preview, isPreviewing: false });
      this.fetchHistory();
    });

    this._previewRequest.request.fail((xhr) => {
      if (xhr?.aborted) {
        return;
      }

      this.setState({ previewError: getErrorMessage(xhr), isPreviewing: false });
      this.fetchHistory();
    });
  };

  renderCount(label, value) {
    return (
      <div className={styles.count} key={label}>
        <span>{translate(label)}</span>
        <strong>{value}</strong>
      </div>
    );
  }

  renderSyncSummary(sync) {
    if (!sync) {
      return null;
    }

    const counts = [
      ['ProcessedItems', sync.processedCount],
      ['NewItems', sync.newCount],
      ['ExistingItems', sync.existingCount],
      ['ChangedItems', sync.changedCount],
      ['ExcludedItems', sync.excludedCount],
      ['UnresolvedItems', sync.unresolvedCount],
      ['SearchEligibleItems', sync.searchEligibleCount]
    ];

    return (
      <div className={styles.summary}>
        <div className={styles.summaryTitle}>
          {translate(sync.result === 'preview' ? 'PreviewComplete' : sync.result === 'failed' ? 'PreviewFailed' : 'ImportListSyncHistory')}
          {sync.failureStage ? ` — ${translate('FailureStage')}: ${sync.failureStage}` : null}
        </div>
        <div className={styles.counts}>
          {counts.map(([label, value]) => this.renderCount(label, value))}
        </div>
      </div>
    );
  }

  renderHistoryRow(sync) {
    const resultLabel = sync.result === 'succeeded'
      ? 'SyncSucceeded'
      : sync.result === 'failed'
        ? 'SyncFailed'
        : sync.result === 'preview'
          ? 'PreviewComplete'
          : 'SyncRunning';

    return (
      <div className={styles.historyRow} key={sync.id}>
        <div className={styles.historyStatus}>{translate(resultLabel)}</div>
        <div className={styles.historyDate}>{new Date(sync.startedAt).toLocaleString()}</div>
        <div className={styles.historyDuration}>{formatDuration(sync.durationMilliseconds)}</div>
        <div className={styles.historyCounts}>
          {translate('ProcessedItems')}: {sync.processedCount}, {translate('NewItems')}: {sync.newCount}, {translate('ChangedItems')}: {sync.changedCount}
        </div>
        <div className={styles.historyState}>
          {sync.failureStage ? `${translate('FailureStage')}: ${sync.failureStage}` : null}
          {sync.providerStateCommitted ? translate('ProviderStateCommitted') : sync.dryRun ? translate('DryRunNoProviderCommit') : null}
        </div>
      </div>
    );
  }

  render() {
    const { id, name, isOpen, onModalClose } = this.props;
    const {
      isLoadingHistory,
      historyError,
      history,
      isPreviewing,
      previewError,
      preview
    } = this.state;

    return (
      <Modal
        isOpen={isOpen}
        onModalClose={onModalClose}
        size={sizes.LARGE}
      >
        <ModalContent onModalClose={onModalClose}>
          <ModalHeader>{translate('ImportListSyncHistory')}: {name}</ModalHeader>

          <ModalBody>
            <div className={styles.actions}>
              <Button
                kind={kinds.PRIMARY}
                isDisabled={isPreviewing}
                onPress={this.onPreviewPress}
              >
                {isPreviewing ? translate('PreviewingSync') : translate('PreviewNextSync')}
              </Button>
            </div>

            {previewError ? <div className={styles.error}>{previewError}</div> : null}
            {this.renderSyncSummary(preview)}

            <h3 className={styles.historyTitle}>{translate('History')}</h3>
            {isLoadingHistory ? <LoadingIndicator /> : null}
            {historyError ? <div className={styles.error}>{historyError}</div> : null}
            {!isLoadingHistory && !historyError && history.length === 0 ? (
              <div className={styles.empty}>{translate('NoImportListSyncHistory')}</div>
            ) : null}
            <div className={styles.history}>
              {history.map((sync) => this.renderHistoryRow(sync))}
            </div>
          </ModalBody>

          <ModalFooter>
            <Button onPress={onModalClose}>{translate('Close')}</Button>
          </ModalFooter>
        </ModalContent>
      </Modal>
    );
  }
}

ImportListSyncModal.propTypes = {
  id: PropTypes.number.isRequired,
  name: PropTypes.string.isRequired,
  isOpen: PropTypes.bool.isRequired,
  onModalClose: PropTypes.func.isRequired
};

export default ImportListSyncModal;
